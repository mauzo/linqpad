<Query Kind="Program">
  <Reference Relative="..\rx\pub\rx.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\rx.dll</Reference>
  <Reference Relative="..\rx\pub\System.Reactive.dll">C:\Users\Ben\Desktop\src\cs\rx\pub\System.Reactive.dll</Reference>
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
	//if (path == "/token")
		//new StackTrace(true).ToString().Dump(id.ToString());
	
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
	
IObservable<string> NewToken (string bad)
{
	tokenRequest.OnNext(bad);
	return token;
}

IObservable<Result> RequestWithToken<Result> (string method, string path)
	=> token
		.SelectMany(tok => MakeRequest<Result>(method, path, tok))
		//.RetryWhen(exs => exs.Case<Exception, string>(sel => sel
		//	.When<TokenFailed>(tf => GetToken(tf.Token))
		//	.Else(ex => Observable.Throw<string>(ex))));
		.RetryWhen(exs => exs.SelectMany(ex => {
			switch (ex) {
			case TokenFailed tf:	return NewToken(tf.Token);
			default:				return Observable.Throw<string>(ex);
			}
		}));
	
class NamedAction {
	public string Name { get; init; }
	public Func<IObservable<string>> Action { get; init; }
}

void SetupTokenSequence ()
{
	IObservable<string> _tokenSource = null;
	var tokenSource = Observable.Defer(() => _tokenSource)
		.Replay(1);
		
	/* We publish null when we have a token request pending. This
	   Where will cause new subscriptions to the token sequence to
	   pause until we have a new token. */
	token = tokenSource
		.Where(t => t != null)
		.Take(1);
		
	tokenRequest = new();
	var newToken = tokenRequest
		.WithLatestFrom(tokenSource, (bad, cur) => new { cur, bad })
		.Where(t => t.cur == t.bad)
		.Select(t => t.cur)
		.DistinctUntilChanged()
		.Select(_ => Unit.Default)
		.StartWith(Unit.Default);
		
	_tokenSource = newToken
		.SelectMany(_ => Observable.Return<string>(null)
			.Concat(MakeRequest<string>("POST", "/token", null)));
	
	//newToken.Connect();
	"Dumping sequences".Dump();
	tokenSource.Dump("tokenSource");
	//token.Dump("token");
	//tokenRequest.Dump("tokenRequest");
	//newToken.Dump("newToken");
	
	"Connecting tokenSource".Dump();
	tokenSource.Connect();
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
	
	SetupTokenSequence();
	
	var acts = RunActions();
	acts.Dump();
	acts.Dump();
	RunActions().Dump();
	
	//Observable.Return(Unit.Default)
	//	.Delay(TimeSpan.FromSeconds(2))
	//	.SelectMany(_ => token)
	//	.Do(t => $"First token: {t}".Dump())
	//	.SelectMany(t => {
	//		tokenRequest.OnNext(t);
	//		return token;
	//	})
	//	.Do(t => $"Second token: {t}".Dump())
	//	.Dump("eval");
}


// You can define other methods, fields, classes and namespaces here