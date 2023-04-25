<Query Kind="Program">
  <Reference Relative="reactive.pub\System.Linq.Async.dll">C:\Users\Ben\Desktop\src\cs\reactive.pub\System.Linq.Async.dll</Reference>
  <Reference Relative="reactive.pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\reactive.pub\System.Reactive.dll</Reference>
</Query>

using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

void Main()
{
	var aen = Observable.Range(1, 500)
		.ToAsyncEnumerable();
		
	Observable.Create<int>(async obs => {
		await foreach (var i in aen) {
			obs.OnNext(i);
			await Task.Delay(TimeSpan.FromSeconds(1));
		}
	}).Dump();
}

// You can define other methods, fields, classes and namespaces here