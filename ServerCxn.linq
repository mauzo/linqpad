<Query Kind="Program">
  <Reference Relative="rx\pub\Microsoft.Windows.SDK.NET.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\Microsoft.Windows.SDK.NET.dll</Reference>
  <Reference Relative="rx\pub\rx.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\rx.dll</Reference>
  <Reference Relative="rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
  <Reference Relative="rx\pub\WinRT.Runtime.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\WinRT.Runtime.dll</Reference>
</Query>

using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Mauzo.Rx;

class Server
{
	public event Action<string> Data;
	public event Action<bool> Disconnected;
	
	private int cxn = 0;
	private bool authDone;
	private SingleAssignmentDisposable sub;
	
	public void Connect ()
	{
		if (sub != null) throw new Exception("Two connections");
		int id = cxn++;
		int stop = Random.Shared.Next(5) + 2;
		sub = new SingleAssignmentDisposable();
		sub.Disposable = Observable.Interval(TimeSpan.FromMilliseconds(500))
			.Take(stop)
			.Subscribe(
				ix => Data?.Invoke($"{id}:{ix}"),
				() => Disconnected?.Invoke(Random.Shared.Next(2) > 0));
		sub.Dump("Connected:");
	}
	
	public void Disconnect ()
	{
		if (sub == null) {
			"Not connected".Dump();
		}
		else {
			sub.Dump("Disconnecting:");
			sub.Dispose();
			sub = null;
		}
	}
}

class XDisconnect : Exception { }
class XHardDisconnect : XDisconnect { }

class ServerCxn
{
	public IObservable<string> Data;

	private Server srv;
	
	public ServerCxn ()
	{
		srv = new Server();
		Data = ConnectServer()
			.RetryWhen(CaseRetryLogic);
	}
	
	public IObservable<string> ConnectServer ()
	{
		return Observable.Defer<string>(() => {
			var discon = Observable.FromEvent<bool>(
					h => srv.Disconnected += h,
					h => srv.Disconnected -= h);
			var data = Observable.FromEvent<string>(
					h => srv.Data += h,
					h => srv.Data -= h)
				.Merge(discon.SelectMany(hard => 
					Observable.Throw<string>(
						hard ? new XHardDisconnect()
							: new XDisconnect())))
				.Finally(() => srv.Disconnect());
			srv.Connect();
			return data;
		});
	}

	private IObservable<long> CaseRetryLogic(IObservable<Exception> errors)
		=> errors.Case<Exception, long>(sel => sel
			.When<XHardDisconnect>(err => 
				Observable.Timer(TimeSpan.FromSeconds(1.0))
					.Do(_ => "Hard disconnect".Dump()))
			.When<XDisconnect>(0)
			.Else(err => Observable.Throw<long>(err)));
	
	private IObservable<Unit> VanillaRetryLogic (IObservable<Exception> errors)
		=> errors.SelectMany(err => {
			switch (err) {
				case XHardDisconnect:
					return Observable.Return(Unit.Default)
						.Do(_ => "Hard disconnect".Dump())
						.Delay(TimeSpan.FromMilliseconds(1000));
				case XDisconnect:
					return Observable.Return(Unit.Default);
				default:
					err.Dump("Exception:");
					return Observable.Throw<Unit>(err);
			}
		});
}


void Main()
{
	var srv = new ServerCxn();
	srv.Data.Take(20).Dump();
}

// You can define other methods, fields, classes and namespaces here