<Query Kind="Program">
  <Reference Relative="rx\pub\Microsoft.Windows.SDK.NET.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\Microsoft.Windows.SDK.NET.dll</Reference>
  <Reference Relative="rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
  <Reference Relative="rx\pub\WinRT.Runtime.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\WinRT.Runtime.dll</Reference>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
  <Namespace>System.Reactive.Subjects</Namespace>
</Query>

static class ObsExt {
	public static IObservable<Unit> AsUnitObservable<T> (this IObservable<T> self)
		=> self.Select(_ => Unit.Default);
}

class Datum  {
	public int Name { get; init; }

	private BehaviorSubject<int> _one;
	public int One {
		get => _one.FirstAsync().Wait();
		set => _one.OnNext(value);
	}
	
	private BehaviorSubject<int> _two;
	public int Two {
		get => _two.FirstAsync().Wait();
		set => _two.OnNext(value);
	}
	
	public IObservable<Unit> Changed
		=> Observable.Merge(_one, _two)
			.Select(_ => Unit.Default);
	
	public Datum ()
	{
		_one = new(0);
		_two = new(0);
	}
}

class Data
{
	public Dictionary<int, Datum> data;
	
	private Subject<Datum> added;
	public IObservable<Unit> Added
		=> added.AsUnitObservable();
	public IObservable<Unit> Changed
		=> added.Select(d => d.Changed).Merge();
	
	public Data ()
	{
		data = new();
		added = new();
	}
	
	public override string ToString ()
		=> String.Join(", ",
			data.OrderBy(e => e.Key)
				.Select(e => e.Value)
				.Select(d => $"{d.Name}: ({d.One}, {d.Two})"));
	
	public Datum Get (int k)
	{
		data.TryGetValue(k, out Datum d);
		if (d == null) {
			d = new Datum { Name = k };
			data[k] = d;
			added.OnNext(d);
		}
		return d;
	}
}

enum Up { One, Two }

struct Update {
	public Up type {get;set;}
	public int name {get;set;}
	public int value {get;set;}
}

void ApplyUpdate (Data data, Update u)
{
	Datum d = data.Get(u.name);
	switch (u.type) {
		case Up.One: 
			d.One = u.value; break;
		case Up.Two:
			d.Two = u.value; break;
	}
}

void Main()
{
	var rng = new Random();
	var data = new Data();
	
	data.Changed
		.Select(_ => data.ToString())
		.Dump();
	
	Observable.Interval(TimeSpan.FromSeconds(0.3))
		.Take(20)
		.Select(_ => new Update {
			type = rng.Next(2) == 0 ? Up.One : Up.Two,
			name = rng.Next(5),
			value = rng.Next(10),
		})
		.Do(u => u.Dump())
		.Subscribe(
			u => ApplyUpdate(data, u),
			() => data.Dump());
}

