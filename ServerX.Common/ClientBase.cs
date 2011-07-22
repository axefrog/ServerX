using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Timers;

namespace ServerX.Common
{
	public abstract class ClientBase<TClient, TChannel, TCallback> : DuplexClientBase<TChannel>, IServiceXBase
		where TClient : ClientBase<TClient, TChannel, TCallback>
		where TChannel : class, IServiceXBase
		where TCallback : class, new()
	{
		private Guid? _id;
		public Guid ClientID
		{
			get
			{
				if(!_id.HasValue)
					throw new InvalidOperationException("Call RegisterClient() before reading the ID property");
				return _id.Value;
			}
			private set { _id = value; }
		}

		protected ClientBase()
			: this(new TCallback())
		{
		}

		protected ClientBase(string endpointConfigurationName)
			: this(endpointConfigurationName, new TCallback())
		{
		}

		private ClientBase(TCallback callback)
			: base(callback)
		{
			Construct(callback);
		}

		protected ClientBase(string endpointConfigurationName, TCallback callback)
			: base(callback, endpointConfigurationName)
		{
			Construct(callback);
		}

		protected ClientBase(object callbackInstance, Binding binding, EndpointAddress remoteAddress)
			: base(callbackInstance, binding, remoteAddress)
		{
			Construct((TCallback)callbackInstance);
		}

		protected void Construct(TCallback callback)
		{
			if(!(this is TClient))
				throw new InvalidOperationException("Cannot inherit from ClientBase<TClient, ...> unless the derived type is of type TClient");
			InnerDuplexChannel.OperationTimeout = new TimeSpan(0, 5, 0);
			InnerDuplexChannel.Closing += OnInnerDuplexChannelClosing;
			InnerDuplexChannel.Opening += OnInnerDuplexChannelOpening;
			InnerDuplexChannel.Faulted += OnInnerDuplexChannelFaulted;
			_keepAliveTimer.Elapsed += OnKeepAliveTimerElapsed;
			InitCallback(callback);
		}

		protected abstract void InitCallback(TCallback callback);

		private Timer _keepAliveTimer = new Timer(5000);
		private void OnInnerDuplexChannelOpening(object sender, EventArgs e)
		{
			_keepAliveTimer.Start();
		}

		void OnInnerDuplexChannelClosing(object sender, EventArgs e)
		{
			OnDisconnected();
			_keepAliveTimer.Stop();
		}

		public Exception LastException { get; private set; }

		void OnInnerDuplexChannelFaulted(object sender, EventArgs e)
		{
			OnDisconnected();
			_keepAliveTimer.Stop();
		}

		void OnDisconnected()
		{
			var handler = Disconnected;
			if(handler != null)
				handler((TClient)this);
		}

		void OnKeepAliveTimerElapsed(object sender, ElapsedEventArgs e)
		{
			KeepAlive();
		}

		public event ClientDisconnectedHandler<TClient, TChannel, TCallback> Disconnected;
		void IServiceXBase.RegisterClient(Guid id)
		{
			ClientID = id;
			Channel.RegisterClient(id);
		}

		public void RegisterClient()
		{
			((IServiceXBase)this).RegisterClient(Guid.NewGuid());
		}

		public void KeepAlive()
		{
			try
			{
				if(State == CommunicationState.Opened)
					try
					{
						Channel.KeepAlive();
					}
					catch(ObjectDisposedException)
					{
					}
			}
			catch(ProtocolException)
			{
				TimedOut = true;
				try { Close(); } catch { }
			}
			catch(TimeoutException)
			{
				TimedOut = true;
				try { Close(); } catch { }
			}
		}

		public bool TimedOut { get; set; }
	}

	public delegate void ClientDisconnectedHandler<TClient, TChannel, TCallback>(TClient client)
		where TClient : ClientBase<TClient, TChannel, TCallback>
		where TChannel : class, IServiceXBase
		where TCallback : class, new();
}
