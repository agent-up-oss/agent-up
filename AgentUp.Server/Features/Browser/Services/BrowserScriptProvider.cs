using System.Text.Json;

namespace AgentUp.Server.Features.Browser.Services;

internal static class BrowserScriptProvider
{
    public const string InspectPage =
        "JSON.stringify((function(){" +
        "var els=Array.from(document.querySelectorAll('a,button,input,select,textarea,[role]'))" +
        ".filter(function(e){return e.offsetParent!==null}).slice(0,100)" +
        ".map(function(e){" +
        "var o={tag:e.tagName.toLowerCase()};" +
        "var r=e.getAttribute('role');if(r)o.role=r;" +
        "var t=(e.textContent||'').trim().replace(/\\s+/g,' ').slice(0,100);if(t)o.text=t;" +
        "if(e.id)o.id=e.id;" +
        "if(e.name)o.name=e.name;" +
        "if(e.type)o.type=e.type;" +
        "if(e.href)o.href=e.href;" +
        "if(e.placeholder)o.placeholder=e.placeholder;" +
        "var sensitive={password:1,hidden:1,token:1};" +
        "var ty=(e.type||'').toLowerCase();" +
        "if(e.value!==undefined&&e.tagName!=='BUTTON'&&!sensitive[ty])o.value=e.value;" +
        "var l=e.getAttribute('aria-label');if(l)o.ariaLabel=l;" +
        "return o;});" +
        "return{" +
        "title:document.title," +
        "url:window.location.href," +
        "headings:Array.from(document.querySelectorAll('h1,h2,h3'))" +
        ".map(function(h){return{level:h.tagName,text:h.textContent.trim().slice(0,100)}})" +
        ".slice(0,20)," +
        "interactive:els};" +
        "})())";

    public const string GetUrl = "window.location.href";

    public const string CheckNavigation = "document.readyState";

    public static string Click(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}}); " +
        $"e.scrollIntoView({{block:'center'}});e.click();" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string Fill(string selector, string text) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}}); " +
        $"var p=e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:" +
        $"e instanceof HTMLSelectElement?HTMLSelectElement.prototype:HTMLInputElement.prototype;" +
        $"var nv=Object.getOwnPropertyDescriptor(p,'value');" +
        $"if(nv&&nv.set)nv.set.call(e,{Js(text)});else e.value={Js(text)};" +
        $"e.dispatchEvent(new Event('input',{{bubbles:true}}));" +
        $"e.dispatchEvent(new Event('change',{{bubbles:true}}));" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string Press(string key) =>
        $"(function(){{" +
        $"var e=document.activeElement||document.body;" +
        $"['keydown','keypress','keyup'].forEach(function(t){{" +
        $"e.dispatchEvent(new KeyboardEvent(t,{{key:{Js(key)},bubbles:true,cancelable:true}}));" +
        $"}});" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string CheckSelector(string selector) =>
        $"!!document.querySelector({Js(selector)})";

    public static string CheckText(string text) =>
        $"(document.body.innerText||'').includes({Js(text)})";

    private static string Js(string value) => JsonSerializer.Serialize(value);
}
