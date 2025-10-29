
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Options;
using sg.gov.cpf.esvc.sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Configurations;
using sg.gov.cpf.esvc.smpp.server.Interfaces;
using sg.gov.cpf.esvc.smpp.server.Models;
using sg.gov.cpf.esvc.smpp.server.Services;
using Ssg.gov.cpf.esvc.smpp.server.Middlewares;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace sg.gov.cpf.esvc.smpp.server.BackgroundServices
{

	public class SmppServer : BackgroundService
	{
		private readonly ILogger<SmppServer> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly SmppServerConfiguration _serverConfiguration;
		private readonly ConcurrentDictionary<string, ISmppSession> _sessions;
		private readonly SemaphoreSlim _connectionLimiter;
		private readonly PduProcessingMiddleware _processingPipeline;
		private readonly IKeyVaultService _keyVaultService;
		private readonly EnvironmentVariablesConfiguration _environmentVariables;

		private readonly SslConfiguration _sslConfig;
		private readonly ISslCertificateManager _certificateManager;

		private TcpListener? _plainTextListener;
		private TcpListener? _sslListener;
		private X509Certificate2? _serverCertificate;

		private readonly TelemetryClient _telemetryClient;

		public SmppServer(
			ILogger<SmppServer> logger,
			IServiceProvider serviceProvider,
			IOptions<SmppServerConfiguration> config,
			IOptions<SslConfiguration> sslConfig,
			ISslCertificateManager certificateManager,
			TelemetryClient telemetryClient,
            IKeyVaultService keyVaultService,
			EnvironmentVariablesConfiguration environmentVariables
        )
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
			_serverConfiguration = config.Value ?? throw new ArgumentNullException(nameof(config));
			_sslConfig = sslConfig.Value ?? throw new ArgumentNullException(nameof(sslConfig));
			_certificateManager = certificateManager ?? throw new ArgumentNullException(nameof(certificateManager));
			_telemetryClient = telemetryClient;
			_sessions = new ConcurrentDictionary<string, ISmppSession>();
			_connectionLimiter = new SemaphoreSlim(_serverConfiguration.MaxConcurrentConnections);
			_keyVaultService = keyVaultService;
			_environmentVariables = environmentVariables;

			// Build processing pipeline using Chain of Responsibility pattern
			_processingPipeline = BuildProcessingPipeline();
        }

        public bool IsRunning { get; private set; }
		public int ActiveSessionsCount => _sessions.Count;


		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			try
			{
				using var telemetryOption = _telemetryClient.StartOperation<RequestTelemetry>(nameof(SmppServer));	

				// Start cleanup background task
				_ = Task.Run(() => RunCleanupJob(stoppingToken), stoppingToken);

				// Load SSL certificate if SSL is enabled
				if (_environmentVariables.IsEnabledSSL)
				{
					_serverCertificate = await _certificateManager.LoadServerCertificateAsync(_keyVaultService.GetSSLCertificate());
					_logger.LogInformation("SSL certificate loaded");
				}

				// Start listeners
				await StartListenersAsync(stoppingToken);

				IsRunning = true;

				// Keep running until cancellation
				while (!stoppingToken.IsCancellationRequested)
				{
					await Task.Delay(1000, stoppingToken);
				}
			}
			catch (OperationCanceledException ex)
			{   
				_telemetryClient.TrackTrace("SMPP Server execution cancelled");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fatal error in Enhanced SMPP server");
			}
			finally
			{
				IsRunning = false;
				await StopListenersAsync();
				await CloseAllSessionsAsync();
			}
		}



		/// <summary>
		/// Handle individual client connection with optional SSL
		/// </summary>
		private async Task HandleClientAsync(TcpClient tcpClient, bool useSsl, CancellationToken cancellationToken)
		{
			ISmppSession? session = null;

			try
			{
				var endpoint = tcpClient.Client?.RemoteEndPoint?.ToString() ?? "Unknown";

				// Create appropriate session type
				if (useSsl && _serverCertificate != null)
				{
					var sslSession = new SslSmppSession(
						tcpClient,
						_logger,
						Options.Create(_sslConfig),
						_serverCertificate);

					// Initialize SSL connection
					await sslSession.InitializeSslAsync(cancellationToken);
					session = sslSession;
				}
				else
				{
					session = new SmppSession(tcpClient, _logger, _telemetryClient);
					_logger.LogInformation("Plain text session {SessionId} established from {Endpoint}",
						session.Id, endpoint);
				}

				// Process PDUs
				await ProcessSessionAsync(session, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				_logger.LogDebug("Client session handling cancelled");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error handling client session {SessionId}", session?.Id ?? "Unknown");
			}
			finally
			{
				// Cleanup session
				if (session != null)
				{
					if (!string.IsNullOrEmpty(session.SystemId))
					{
						_sessions.TryRemove(session.SystemId, out _);
					}

					session.Dispose();
				}

				tcpClient.Dispose();
			}
		}

		/// <summary>
		/// Process PDUs for a session
		/// </summary>
		private async Task ProcessSessionAsync(ISmppSession session, CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested &&
                   (session is SmppSession regularSession && regularSession.IsConnected ||
					session is SslSmppSession sslSession && sslSession.IsSslAuthenticated))
			{
				try
				{
                    _telemetryClient.Context.Operation.Id = session.ProcessId.ToString();

                    var pdu = await session.ReadPduAsync(cancellationToken);
					if (pdu == null)
					{
						_logger.LogDebug("Session {SessionId} - No PDU received, ending connection", session.Id);
						break;
					}

					// Process PDU through middleware pipeline
					var response = await _processingPipeline.HandleAsync(pdu, session, cancellationToken);

					if (response != null)
					{
						await session.SendPduAsync(response, cancellationToken);
					}

					// Register authenticated sessions
					if (session.IsAuthenticated && !string.IsNullOrEmpty(session.SystemId) &&
						!_sessions.ContainsKey(session.SystemId))
					{
						_sessions.TryAdd(session.SystemId, session);
					}
				}
				catch (OperationCanceledException)
				{
					_logger.LogDebug("Session PDU processing cancelled for {SessionId}", session.Id);
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error processing PDU for session {SessionId}", session.Id);
					break;
				}
			}
		}

		/// <summary>
		/// Build the PDU processing pipeline
		/// </summary>
		private PduProcessingMiddleware BuildProcessingPipeline()
		{
			// Build middleware chain: Logging -> Authentication -> Handler
			var logging = new LoggingMiddleware(_serviceProvider.GetRequiredService<ILogger<LoggingMiddleware>>());
			var handler = new HandlerMiddleware(_serviceProvider, _serviceProvider.GetRequiredService<ILogger<HandlerMiddleware>>(), _serverConfiguration);
			var auth = new SmppAuthenticationMiddleware(_serviceProvider.GetRequiredService<ILogger<SmppAuthenticationMiddleware>>());

			// Chain them together
			logging.SetNext(auth).SetNext(handler);

			_logger.LogDebug("PDU processing pipeline built with SSL support");

			return logging;
		}


		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			IsRunning = false;
			await StopListenersAsync();
			await CloseAllSessionsAsync();
			_connectionLimiter.Dispose();

			await base.StopAsync(cancellationToken);

		}

		/// <summary>
		/// Start both plain text and SSL listeners
		/// </summary>
		private async Task StartListenersAsync(CancellationToken cancellationToken)
		{
			// Start SSL listener if enabled
			if (_environmentVariables.IsEnabledSSL && _serverCertificate != null)
			{
				_sslListener = new TcpListener(IPAddress.Any, _sslConfig.Port);
				_sslListener.Start();
                _telemetryClient.TrackTrace($"SSL SMPP listener started on port {_sslConfig.Port}");

				await AcceptConnectionsAsync(_sslListener, true, cancellationToken);
			}
			else
			{
                // Start plain text listener
                _plainTextListener = new TcpListener(IPAddress.Any, _serverConfiguration.Port);
                _plainTextListener.Start();
                _telemetryClient.TrackTrace($"Plain text SMPP listener started on port {_serverConfiguration.Port}");

                await AcceptConnectionsAsync(_plainTextListener, false, cancellationToken);
            }
		}


		/// <summary>
		/// Stop all listeners
		/// </summary>
		private async Task StopListenersAsync()
		{
			try
			{
				_plainTextListener?.Stop();
				_sslListener?.Stop();

				_telemetryClient.TrackTrace("SMPP listeners stopped");

				await Task.CompletedTask; // For consistency
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error stopping SMPP listeners");
			}
		}

		/// <summary>
		/// Close all active sessions gracefully
		/// </summary>
		private async Task CloseAllSessionsAsync()
		{
			_logger.LogInformation("Closing {SessionCount} active sessions", _sessions.Count);

			var closeTasks = _sessions.Values.Select(async session =>
			{
				try
				{
					session.Close();
					await Task.Delay(100); // Give time for graceful close
					session.Dispose();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error closing session {SessionId}", session.Id);
				}
			});

			await Task.WhenAll(closeTasks);
			_sessions.Clear();
		}

		/// <summary>
		/// Accept connections on the specified listener
		/// </summary>
		private async Task AcceptConnectionsAsync(TcpListener listener, bool useSsl, CancellationToken cancellationToken)
		{
			var listenerType = useSsl ? "SSL" : "Plain";

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					// Wait for connection slot
					await _connectionLimiter.WaitAsync(cancellationToken);

					var tcpClient = await listener.AcceptTcpClientAsync(cancellationToken);

					// Handle client in background task
					_ = Task.Run(async () =>
					{
						try
						{
							await HandleClientAsync(tcpClient, useSsl, cancellationToken);
						}
						finally
						{
							_connectionLimiter.Release();
						}
					}, cancellationToken);
				}
				catch (OperationCanceledException)
				{
                    _telemetryClient.TrackTrace($"{listenerType} connection acceptance cancelled");
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error accepting {ListenerType} connection", listenerType);
					_connectionLimiter.Release();

					// Brief delay to prevent tight loop on persistent errors
					await Task.Delay(1000, cancellationToken);
				}
			}
		}

		/// <summary>
		/// Background job for cleanup tasks
		/// </summary>
		private async Task RunCleanupJob(CancellationToken cancellationToken)
		{
			var cleanupInterval = TimeSpan.Parse(_serverConfiguration.CleanUpJobInterval);
			var staleInterval = TimeSpan.Parse(_serverConfiguration.StaleCleanUpInterval);

			_logger.LogInformation("Cleanup job started (Interval: {CleanupInterval}, Stale: {StaleInterval})",
				cleanupInterval, staleInterval);

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					await Task.Delay(cleanupInterval, cancellationToken);

					_logger.LogDebug("Running cleanup job at {Time}", DateTimeOffset.Now);

					// Clean up stale message parts
					var messageTracker = _serviceProvider.GetRequiredService<MessageTracker>();
					messageTracker.CleanUpStaleMessagesParts(staleInterval);

					
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in cleanup job");
				}
			}
		}
	}
}
