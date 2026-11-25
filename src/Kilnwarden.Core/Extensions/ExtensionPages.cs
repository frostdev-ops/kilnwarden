// SPDX-License-Identifier: MIT
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kilnwarden.Api;
using Kilnwarden.Http;
using Kilnwarden.Routing;
using Kilnwarden.Security;

namespace Kilnwarden.Extensions;

/// <summary>What extension pages answer: a declarative page's layout (only the blocks the user may use), and a custom
/// page's sandboxed document.</summary>
public static class ExtensionPages
{
    /// <summary>The sandbox of a custom page: scripts only (no same-origin, so no cookies, storage or panel DOM), no
    /// fetch, XHR, WebSocket, forms, popups or top navigation, nothing but the panel may frame it. CSP cannot stop a frame
    /// from navigating itself: the panel's own frame-src 'self' keeps it on the panel, and the host stops answering a frame
    /// whose document changed (web/src/ext/bridge.ts frameGuard; review S6). The bootstrap and the mod's script carry the
    /// nonce.</summary>
    public static string FramePolicy(string nonce) =>
        $"sandbox allow-scripts; default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; img-src data: blob:; " +
        "font-src 'none'; connect-src 'none'; media-src 'none'; object-src 'none'; worker-src 'none'; frame-src 'none'; form-action 'none'; " +
        "base-uri 'none'; frame-ancestors 'self'";

    public static JsonObject Layout(ExtensionModule m, DeclarativePage page, IPrincipal user)
    {
        string Path(string kind, string name) => $"/api/{m.Id}/{kind}/{name}";
        var blocks = new JsonArray();
        foreach (var b in page.Blocks)
        {
            JsonObject? o = b switch
            {
                StatBlock s when m.FindQuery(s.Query) is { } q && user.Has(q.Permission) =>
                    new() { ["type"] = "stat", ["title"] = s.Title, ["path"] = Path("data", s.Query), ["field"] = s.Field, ["unit"] = s.Unit },
                TableBlock t when m.FindQuery(t.Query) is { } q && user.Has(q.Permission) =>
                    new()
                    {
                        ["type"] = "table", ["title"] = t.Title, ["path"] = Path("data", t.Query), ["rows"] = t.RowsField,
                        ["columns"] = new JsonArray([.. t.Columns.Take(16).Select(c => (JsonNode)new JsonObject { ["field"] = c.Field, ["title"] = c.Title })]),
                    },
                FormBlock f when m.FindAction(f.Action) is { } a && user.Has(a.Permission) =>
                    new()
                    {
                        ["type"] = "form", ["title"] = f.Title ?? a.Title ?? a.Name, ["path"] = Path("actions", f.Action), ["submit"] = f.Submit ?? "Run",
                        ["schema"] = a.ArgsSchema?.DeepClone(), ["confirm"] = a.Confirm,
                    },
                ButtonBlock btn when m.FindAction(btn.Action) is { } a && user.Has(a.Permission) =>
                    new() { ["type"] = "button", ["label"] = btn.Label, ["path"] = Path("actions", btn.Action), ["args"] = btn.Args?.DeepClone(), ["confirm"] = a.Confirm },
                TextBlock tx => new() { ["type"] = "text", ["text"] = tx.Text.Length > 4000 ? tx.Text[..4000] : tx.Text },
                _ => null,
            };
            if (o is not null) blocks.Add(o);
        }
        return new JsonObject { ["module"] = m.Id, ["id"] = page.Id, ["title"] = page.Title, ["blocks"] = blocks };
    }

    /// <summary>The custom page's document: the host bridge, then the mod's ES module, both inline with a fresh nonce,
    /// served with &lt;see cref="FramePolicy"/&gt;. What the bridge may name (this module's actions, queries and topics
    /// the user has) goes in as data; the host page checks every message again and the server checks every call.</summary>
    public static HttpResponse Frame(ExtensionModule m, CustomPage page, RequestContext ctx)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var user = ctx.User!;
        var allowed = new JsonObject
        {
            ["module"] = m.Id,
            ["origin"] = ctx.Origin,
        };
        var html = new StringBuilder(page.Script.Length + 4096);
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append("<title>").Append(WebUtility.HtmlEncode(page.Title)).Append("</title>");
        html.Append("<style nonce=\"").Append(nonce).Append("\">").Append(BaseStyle).Append(Inline(page.Style ?? "", "style")).Append("</style>");
        html.Append("<script nonce=\"").Append(nonce).Append("\">window.__KWA__=").Append(Inline(allowed.ToJsonString(), "script")).Append(';').Append(Bridge).Append("</script>");
        html.Append("<script type=\"module\" nonce=\"").Append(nonce).Append("\">").Append(Inline(page.Script, "script")).Append("</script>");
        html.Append("</head><body></body></html>");
        var r = HttpResponse.Html(200, html.ToString());
        r.Headers.Set("Content-Security-Policy", FramePolicy(nonce));
        r.Headers.Set("X-Frame-Options", "SAMEORIGIN");
        r.Headers.Set("Cache-Control", "no-store");
        _ = user;
        return r;
    }

    /// <summary>Keeps inline text from closing its element ("&lt;/script", "&lt;/style", "&lt;!--").</summary>
    internal static string Inline(string text, string element) =>
        text.Replace("</" + element, "<\\/" + element, StringComparison.OrdinalIgnoreCase).Replace("<!--", "<\\!--", StringComparison.Ordinal);

    private const string BaseStyle =
        ":root{color-scheme:dark light;font:14px/1.45 system-ui,sans-serif}body{margin:0;padding:12px;background:transparent;color:var(--kwa-text,#e6edf3)}";

    /// <summary>window.kiln: call(action, args), query(name, params), subscribe(topic, fn) -> unsubscribe, onTheme(fn),
    /// resize(height). Messages go to the panel's origin only; answers are taken only from the parent window.</summary>
    private const string Bridge = """
(()=>{const C=window.__KWA__;let n=0;const wait=new Map(),subs=new Map(),themes=[];
const post=m=>parent.postMessage(Object.assign({kwa:1},m),C.origin);
addEventListener("message",e=>{if(e.source!==parent)return;const m=e.data;if(!m||m.kwa!==1)return;
if(m.type==="result"&&wait.has(m.id)){const w=wait.get(m.id);wait.delete(m.id);m.ok?w.res(m.result):w.rej(new Error(m.error||"failed"));}
else if(m.type==="event"){(subs.get(m.topic)||[]).forEach(f=>{try{f(m.payload,m)}catch(_){}})}
else if(m.type==="theme"){themes.forEach(f=>{try{f(m.tokens)}catch(_){}});for(const[k,v]of Object.entries(m.tokens||{}))if(/^--[a-z0-9-]+$/.test(k)&&typeof v==="string")document.documentElement.style.setProperty(k,v)}});
const ask=(type,body)=>new Promise((res,rej)=>{const id=++n;wait.set(id,{res,rej});post(Object.assign({id,type},body))});
window.kiln=Object.freeze({module:C.module,
call:(action,args)=>ask("call",{action,args:args||{}}),
query:(query,params)=>ask("query",{query,params:params||{}}),
subscribe:(topic,fn)=>{if(!subs.has(topic)){subs.set(topic,[]);post({type:"subscribe",topic})}subs.get(topic).push(fn);
return()=>{const l=subs.get(topic)||[];const i=l.indexOf(fn);if(i>=0)l.splice(i,1);if(!l.length){subs.delete(topic);post({type:"unsubscribe",topic})}}},
onTheme:fn=>{themes.push(fn)},
resize:h=>post({type:"resize",height:Math.max(0,Math.min(20000,Math.round(+h||0)))})});
post({type:"ready"});})();
""";
}
