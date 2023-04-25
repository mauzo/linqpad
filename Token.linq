<Query Kind="Program">
  <Reference Relative="rx\pub\rx.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\rx.dll</Reference>
  <Reference Relative="rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
</Query>

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;

using System.Net;
using System.Net.Http;
using System.Text.Json;

using Mauzo.Rx;

HttpClient client;
Uri baseUrl;

IObservable<string> token;
Subject<string> tokenRequest;

class TokenFailed : Exception {
	public string Token { get; init; }
}

async Task<Result> _MakeRequest<Result>(string method, string path, string token)
{
	var id = Guid.NewGuid();
	var url = new Uri(baseUrl, path);
	var req = new HttpRequestMessage(new HttpMethod(method), url);
	
	$"Request [{id}]: {url} ({token})".Dump();
	
	if (token != null)
		req.Headers.Add("Authorization", $"Bearer {token}");
	//req.ToString().Dump();
	
	var res = await client.SendAsync(req);
	if (res.StatusCode == HttpStatusCode.Unauthorized) {
		$"Bad token [{id}]".Dump();
		throw new TokenFailed { Token = token };
	}
	else if (res.StatusCode != HttpStatusCode.OK)
		throw new Exception($"Request failed [{id}]: {res.StatusCode}");
		
	var json = await res.Content.ReadAsStringAsync();
	var rv = JsonSerializer.Deserialize<Result>(json);
	$"Response [{id}]: {rv}".Dump();
	return rv;
}

IObservable<Result> MakeRequest<Result> (string method, string path, string token)
	=> _MakeRequest<Result>(method, path, token).ToObservable();
	
IObservable<string> GetToken (string bad = null)
{
	tokenRequest.OnNext(bad);
	return token;
}

IObservable<Result> RequestWithToken<Result> (string method, string path)
	=> GetToken()
		.SelectMany(tok => MakeRequest<Result>(method, path, tok))
		//.RetryWhen(exs => exs.Case<Exception, string>(sel => sel
		//	.When<TokenFailed>(tf => GetToken(tf.Token))
		//	.Else(ex => Observable.Throw<string>(ex))));
		.RetryWhen(exs => exs.SelectMany(ex =>
			ex is TokenFailed	? GetToken(((TokenFailed)ex).Token)
				: Observable.Throw<string>(ex)));
	
class NamedAction {
	public string Name { get; init; }
	public Func<IObservable<string>> Action { get; init; }
}

IObservable<int> RunActions ()
	=> Observable.Concat<bool>(
		Observable.Return(true),
		Observable.Return(true).Delay(TimeSpan.FromSeconds(6)),
		Observable.Return(true),
		Observable.Return(true).Delay(TimeSpan.FromSeconds(2)))
	.SelectMany(_ => RequestWithToken<int>("GET", "/count"));
	
void Main()
{
	client = new HttpClient();
	baseUrl = new Uri("http://localhost:3000");
	
	IObservable<bool> newToken = null;
	var tokenSource = Observable.Defer(() => newToken)
		.SelectMany(_ => 
			Observable.Return<string>(null)
				.Concat(MakeRequest<string>("POST", "/token", null)))
		.Publish(null);
	token = tokenSource
		.Where(t => t != null)
		.Take(1);
	//tokenSource.Dump();
	
	tokenRequest = new();
	newToken = Observable.Create<string>(obs => {
			$"Observer for newToken: {new StackTrace(true)}".Dump();
			return tokenRequest.Subscribe(obs);
		})
		.Do(r => $"Token request: {r}".Dump())
		.DistinctUntilChanged()
		.SelectMany(req =>
			tokenSource.Take(1).Select(tok => new { req = req, tok = tok }))
		.Select(comb => comb.req == comb.tok)
		.Where(ok => ok);
		//.Publish();
	//tokenRequest.Dump();
		
	tokenSource.Connect();
	//newToken.Connect();
	
	var acts = RunActions();
	acts.Dump();
	acts.Dump();
	RunActions().Dump();
}


// You can define other methods, fields, classes and namespaces here