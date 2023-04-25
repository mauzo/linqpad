<Query Kind="Program">
  <Reference Relative="rx\pub\Newtonsoft.Json.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\Newtonsoft.Json.dll</Reference>
  <Reference Relative="rx\pub\System.Collections.Immutable.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Collections.Immutable.dll</Reference>
  <Reference Relative="rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
</Query>

using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

IObservable<string> Error(string msg)
{
	return Observable.Throw<string>(new Exception(msg));
}

static class ObsEx {
	public static IEnumerable<IObservable<string>> CalculateBackoffs (this IObservable<string> self)
	{
		double delay = 0.5;
		while (true) {
			delay += 0.5;
			yield return self.Catch<string, Exception>(ex => 
				Observable.Concat(
					Observable.Return("ERROR: " + ex.Message),
					Observable.Return($"backoff {delay}"),
					Observable.Empty<string>().Delay(TimeSpan.FromSeconds(delay))));
		}
	}
	
	public static IObservable<string> ReconnectWithBackoff (this IObservable<string> self)
		=> self.CalculateBackoffs().Concat();
		//=> Observable.Create<string>(dest => self
	//			.CalculateBackoffs().Concat()
	//			.Subscribe(dest));
}

IObservable<Unit> BackoffLogic (IObservable<Exception> errors)
	=> errors.Select(e => new { Err = e, Retry = e.Message == "foo" })
		.Let(actions => Observable.Merge(
			actions.Where(a => a.Retry)
				.Scan(0.5, (d, ex) => d + 0.5)
				.Do(d => $"Backoff: {d}".Dump())
				.Select(d => TimeSpan.FromSeconds(d))
				.SelectMany(d => Observable.Timer(d))
				.Do(d => $"Done backoff: {d}".Dump())
				.Select(_ => Unit.Default),
			actions.Where(a => !a.Retry)
				.SelectMany(a => Observable.Throw<Unit>(a.Err))));

//{
//	var actions = errors
//		.Select(e => new { Err = e, Retry = e.Message == "foo" });
//		
//	var retry = actions.Where(a => a.Retry)
//		.Scan(0.5, (d, ex) => d + 0.5)
//		.Do(d => $"Backoff: {d}".Dump())
//		.Select(d => TimeSpan.FromSeconds(d))
//		.SelectMany(d => Observable.Timer(d))
//		.Do(d => $"Done backoff: {d}".Dump())
//		.Select(_ => Unit.Default);
//		
//	var rethrow = actions.Where(a => !a.Retry)
//		.SelectMany(a => Observable.Throw<Unit>(a.Err));
//		
//	return Observable.Merge(retry, rethrow);
//}

void Main()
{
	var rng = new Random();
	var src = Observable.Interval(TimeSpan.FromSeconds(0.5))
		.Select(i => i.ToString())
		.Publish();
	src.Connect();
	
	var cxn = src
		.SelectMany(i => 
			Observable.Return(i)
				.Concat(rng.NextDouble() > 0.3
					? Observable.Empty<string>()
					: Error(rng.NextDouble() > 0.2 ? "foo" : "bar")));
	
	cxn	.RetryWhen(BackoffLogic)
		.Dump();
}
