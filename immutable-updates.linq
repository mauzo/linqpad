<Query Kind="Program">
  <Reference Relative="rx\pub\JsonSubTypes.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\JsonSubTypes.dll</Reference>
  <Reference Relative="rx\pub\Newtonsoft.Json.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\Newtonsoft.Json.dll</Reference>
  <Reference Relative="rx\pub\System.Collections.Immutable.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Collections.Immutable.dll</Reference>
  <Reference Relative="rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
</Query>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using JsonSubTypes;

using Updater = System.Func<object, 
	System.Collections.Generic.IEnumerable<string>, 
	object, object>;

class ModelBase
{
	public readonly Guid uuid = Guid.NewGuid();
	public override string ToString () => uuid.ToString();
}

[DataContract]
class Scene : ModelBase
{
	[DataMember(Name="name")] public string Name { get; init; }
	[DataMember(Name="camera")] public Transform Camera { get; init; }
	[DataMember(Name="sceneObjects")] public ImmutableDictionary<Guid, SceneObject> SceneObjects {get; init;}
	[DataMember(Name="slides")] public ImmutableList<string> Slides {get; init;}
}

[DataContract]
[JsonConverter(typeof(JsonSubtypes), "type")]
class SceneObject : ModelBase
{
	[DataMember(Name="name")] public string Name {get; init; }
}

[DataContract]
class Empty : SceneObject
{
	[DataMember(Name="transform")] public Transform Transform { get; init; }
}

[DataContract]
class Transform : ModelBase {
	[DataMember(Name="position")] public Vector3 Position { get; init; }
	[DataMember(Name="rotation")] public Vector3 Rotation { get; init; }
}

[DataContract]
class Vector3 : ModelBase {
	[DataMember(Name="x")] public float X { get; init; }
	[DataMember(Name="y")] public float Y { get; init; }
	[DataMember(Name="z")] public float Z { get; init; }
}

Func<string, object> FromString (Type typ)
{
	if (typ == typeof(string))
		return s => s;
	if (typ == typeof(Guid))
		return s => Guid.Parse(s);
	throw new Exception($"Can't convert string to {typ}");
}

Updater IDUpdate (Type typ)
{
	var tparams = typ.GetGenericArguments();
	var ktyp = tparams[0];
	var vtyp = tparams[1];
	
	var tokey = FromString(ktyp);
	var get = typ.GetProperty("Item", new Type[1] {ktyp});
	var set = typ.GetMethod("SetItem", new Type[2] {ktyp, vtyp});
	
	return (src, path, value) => {
		var prop = path.First();
		var next = path.Skip(1);
		var key = tokey(prop);
		
		var old = get.GetValue(src, new object[1] {key});
		var upd = Update(old, next, value);
		$"  [{prop}] {old}->{upd}".Dump();
		return set.Invoke(src, new object[2] { key, upd });
	};
}

Updater DCUpdate(Type typ)
{
	var cons = typ.GetConstructor(Type.EmptyTypes);
	var props = typ.GetProperties()
		.Select(p => new {
			Name = p.GetCustomAttribute<DataMemberAttribute>()?.Name,
			Prop = p,
		})
		.Where(p => p.Name != null);
	
	$"  DCUpdate({typ})".Dump();
	return (src, path, value) => {
		var prop = path.First();
		var next = path.Skip(1);
		$"  Prop {prop} Next {String.Join(",",next)}".Dump();
		
		object dest = cons.Invoke(null);
		foreach (var p in props) {
			var old = p.Prop.GetValue(src);
			var upd = p.Name == prop ? Update(old, next, value) : old;
			$"  {p.Prop.Name}: {p.Name}: {old}->{upd}".Dump();
			p.Prop.SetValue(dest, upd);
		}
		return dest;
	};
}

Updater MakeUpdater (Type typ)
{
	if (typ.GetCustomAttribute<DataContractAttribute>() != null)
		return DCUpdate(typ);
	
	var imDict = typeof(ImmutableDictionary<object, object>)
		.GetGenericTypeDefinition();
	
	if (typ.IsGenericType
		&& typ.GetGenericTypeDefinition() == imDict
	) {
		return IDUpdate(typ);
	}
	
	throw new Exception($"Don't know how to update {typ}");
}

static ConcurrentDictionary<Type, Updater> updaters = new();

Updater GetUpdater (Type typ)
	=> updaters.GetOrAdd(typ, MakeUpdater);

object Update (object src, IEnumerable<string> path, object value)
{
	if (!path.Any()) {
		"NO PATH REMAINING".Dump();
		return value;
	}
	var typ = src.GetType();
	$"UPDATE: {typ}".Dump();
	var rv = GetUpdater(typ)(src, path, value);
	"---".Dump();
	return rv;
}

void Main()
{
	var scene = JsonConvert.DeserializeObject<Scene>("""
		{
			"name": "hello",
			"camera": {
				"position": { "x": 4, "y": 5, "z": 6 },
				"rotation": { "x": 0, "y": 0, "z": 0 }
			},
			"sceneObjects": {
				"6dcb947c-7c6b-49bc-8162-01a2d6274eb0": { "type": "Empty",
					"name": "bob",
					"transform": {
						"position": { "x": 1, "y": 2, "z": 3 },
						"rotation": { "x": 0, "y": 0, "z": 0 }
					}
				},
				"13abdf6d-50a2-4254-afa2-31c2832e98c2": { "type": "SceneObject",
					"name": "albert"
				}
			},
			"slides": []
		}
	""");
	scene.Dump();
	var scene2 = Update(scene, "sceneObjects/6dcb947c-7c6b-49bc-8162-01a2d6274eb0/transform/position/x".Split('/'), 44);
	scene2.Dump();
}

// You can define other methods, fields, classes and namespaces here