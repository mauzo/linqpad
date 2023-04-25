<Query Kind="Program">
  <Reference Relative="rx\bin\Debug\net5.0\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\bin\Debug\net5.0\System.Reactive.dll</Reference>
</Query>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

interface IHasKey<Key> {
	public Key key { get; init; }
}

interface IUpdatable {
}

interface IUpdatable<Value> : IUpdatable
{
	void ApplyUpdate(Value update);
}

interface IUpdate<Key>
{
	public Key key { get; }

	void ApplyTo(IUpdatable to);
	void ApplyToDict<Subject> (IDictionary<Key, Subject> dict)
		where Subject : IUpdatable, IHasKey<Key>, new();
	void ApplyToDict<Subject> (IDictionary<Key, Subject> dict, Func<Key, Subject> ctor)	
		where Subject : IUpdatable;
}

class Update<Key, Value> : IUpdate<Key>
{
	public Key key { get; set; }
	public Value value { get; set; }
	
	public void ApplyTo (IUpdatable to)
	{
		var updatable = (IUpdatable<Value>)to;
		updatable.ApplyUpdate(value);
	}
	
	public void ApplyToDict<Subject> (IDictionary<Key, Subject> dict)
		where Subject : IUpdatable, IHasKey<Key>, new()
		=> ApplyToDict(dict, (u) => new Subject { key = u });
	
	public void ApplyToDict<Subject> (IDictionary<Key, Subject> dict, Func<Key, Subject> ctor)
		where Subject : IUpdatable
	{
		Subject subj;
		if (!dict.TryGetValue(key, out subj)) {
			subj = ctor(key);
			dict.Add(key, subj);
		}
		ApplyTo(subj);
	}
}

class DirInfo {
	public string address {get;set;}
	public bool online {get;set;}
}

class GenInfo {
	public string name {get;set;}
}

class Data : IUpdatable<DirInfo>, IUpdatable<GenInfo>
{
	public Guid uuid { get; init; }
	public string name;
	public string address;
		
	void IUpdatable<DirInfo>.ApplyUpdate (DirInfo update)
		=> address = update.online ? update.address : null;
	void IUpdatable<GenInfo>.ApplyUpdate (GenInfo update)
		=> name = update.name;
}

void Main()
{
	var data = new Dictionary<Guid,Data>();
	
	var one = Guid.NewGuid();
	var two = Guid.NewGuid();
	
	var updates = new List<IUpdate<Guid>> {
		new Update<Guid,DirInfo> {
			key = one,
			value = new DirInfo {
				address = "on/two",
				online = true,
			}
		},
		new Update<Guid,GenInfo> { 
			key = two,
			value = new GenInfo { name = "blah" },
		},
		new Update<Guid,DirInfo> {
			key = one,
			value = new DirInfo {
				address = "three",
				online = false,
			},
		},
		new Update<Guid,DirInfo> {
			key = two,
			value = new DirInfo {
				address = "three/four",
				online = true,
			}
		},
		new Update<Guid,GenInfo> {
			key = one,
			value = new GenInfo { name = "fdfdfdfd" },
		},
	};
	
	var rng = new Random();
	Observable.Generate(
			updates,
			us => us.Count > 0,
			us => { us.RemoveAt(0); return us; },
			us => us[0],
			us => TimeSpan.FromSeconds(rng.NextDouble() * 4))
		.Do(u => u.Dump())
		.Subscribe(
			u => u.ApplyToDict(data, u => new Data { uuid = u }),
			() => data.Dump());
}

// You can define other methods, fields, classes and namespaces here