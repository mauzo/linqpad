<Query Kind="Program">
  <Reference Relative="rx\pub\System.Collections.Immutable.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Collections.Immutable.dll</Reference>
  <Namespace>System.Collections.Immutable</Namespace>
  <Namespace>System.Reactive</Namespace>
  <Namespace>System.Reactive.Disposables</Namespace>
  <Namespace>System.Reactive.Linq</Namespace>
</Query>

class Data
{
	public ImmutableDictionary<string, int> Dict { get; private set; }
	public IDictionary<string, int> DictProxy
		=> new DictProxy(Dict, d => Dict = d);
}

class DictProxy : IDictionary<string, int> {
	private ImmutableDictionary<string, int> dict;
	private Action<ImmutableDictionary<string,int>> set_dict;
	
	private ImmutableDictionary<string, int> Dict {
		get => dict;
		set {
			dict = value;
			set_dict(dict);
		}
	}
	
	private T Unimplemented<T> ()
		=> throw new Exception("Unimplemented");
		
	public DictProxy (ImmutableDictionary<string, int> initial,
		Action<ImmutableDictionary<string, int>> set)
	{
		dict = initial;
		set_dict = set;
	}
	
	public int Count => dict.Count;
	public bool IsReadOnly => false;
	public int this[string key] {
		get => Dict[key];
		set => Dict = Dict.SetItem(key, value);
	}
	public ICollection<string> Keys => ((IDictionary<string,int>)dict).Keys;
	public ICollection<int> Values => ((IDictionary<string,int>)dict).Values;
	
	public void Add (string k, int v)
		=> Dict = Dict.Add(k, v);
	public bool ContainsKey (string key)
		=> Dict.ContainsKey(key);
	public bool Remove (string key)
	{
		bool found = dict.ContainsKey(key);
		if (found)
			Dict = Dict.Remove(key);
		return found;
	}
	public bool TryGetValue (string key, out int val)
		=> Dict.TryGetValue(key, out val);
		
	public void Add (KeyValuePair<string, int> kv)
		=> Add(kv.Key, kv.Value);
	public void Clear () => Dict = ImmutableDictionary<string,int>.Empty;
	public bool Contains (KeyValuePair<string, int> kv)
		=> Dict.Contains(kv);
	public void CopyTo (KeyValuePair<string,int>[] array, int ix)
		=> ((ICollection<KeyValuePair<string, int>>)Dict).CopyTo(array, ix);
	public bool Remove (KeyValuePair<string, int> kv)
		=> Unimplemented<bool>();
		
	public IEnumerator<KeyValuePair<string, int>> GetEnumerator ()
		=> Unimplemented<IEnumerator<KeyValuePair<string,int>>>();
	IEnumerator IEnumerable.GetEnumerator()
		=> Unimplemented<IEnumerator>();
}

void Main()
{
	var dict = ImmutableDictionary<string, int>.Empty
		.Add("a", 3)
		.Add("b", 4);
		
	dict.Dump();
	
	((IDictionary)dict).Add("c", 5);
}

// You can define other methods, fields, classes and namespaces here