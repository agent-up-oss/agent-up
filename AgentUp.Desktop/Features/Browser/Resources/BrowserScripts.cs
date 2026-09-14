using System.Text.Json;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Browser.Resources;

internal static class BrowserScripts
{
    internal const int AnimationMs = 500;

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
        "var sensitive={password:1,hidden:1};" +
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

    public const string GetTitle = "document.title";

    public const string CheckNavigation = "document.readyState";

    public static string ClickTarget(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}}); " +
        $"e.scrollIntoView({{block:'center',inline:'center'}});" +
        $"var r=e.getBoundingClientRect();" +
        $"return JSON.stringify({{success:true,url:window.location.href,x:Math.round(r.left+r.width/2),y:Math.round(r.top+r.height/2)}});" +
        $"}})()";

    public static string BeginMouseMove(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}});" +
        $"e.scrollIntoView({{block:'center',inline:'center'}});" +
        $"var r=e.getBoundingClientRect();" +
        $"var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);" +
        $"if(!window.__agentUpMouse)window.__agentUpMouse={{x:Math.round(window.innerWidth/2),y:Math.round(window.innerHeight/2)}};" +
        $"var m=document.getElementById('__agentUpMouse');" +
        $"if(!m){{m=document.createElement('div');m.id='__agentUpMouse';document.documentElement.appendChild(m);}}" +
        $"m.style.cssText='position:fixed;left:'+window.__agentUpMouse.x+'px;top:'+window.__agentUpMouse.y+'px;width:14px;height:14px;margin:-7px 0 0 -7px;border-radius:999px;background:{AgentUpThemeColors.AccentBright};box-shadow:0 0 0 2px color-mix(in srgb,{AgentUpThemeColors.Canvas} 45%,transparent),0 0 18px color-mix(in srgb,{AgentUpThemeColors.AccentBright} 70%,transparent);z-index:2147483647;pointer-events:none;transition:left {AnimationMs}ms linear,top {AnimationMs}ms linear;';" +
        $"requestAnimationFrame(function(){{m.style.left=x+'px';m.style.top=y+'px';window.__agentUpMouse={{x:x,y:y}};}});" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string BeginAttentionPing(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}});" +
        $"e.scrollIntoView({{block:'center',inline:'center'}});" +
        $"var r=e.getBoundingClientRect();" +
        $"var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);" +
        $"var c=document.getElementById('__agentUpClickRing');" +
        $"if(c)c.remove();" +
        $"c=document.createElement('div');c.id='__agentUpClickRing';document.documentElement.appendChild(c);" +
        $"c.style.cssText='position:fixed;left:'+x+'px;top:'+y+'px;width:64px;height:64px;margin:-32px 0 0 -32px;border:3px solid {AgentUpThemeColors.AccentBright};border-radius:999px;z-index:2147483646;pointer-events:none;transform:scale(.35);opacity:.95;transition:transform {AnimationMs}ms ease-out,opacity {AnimationMs}ms ease-out;';" +
        $"requestAnimationFrame(function(){{c.style.transform='scale(1.8)';c.style.opacity='0';}});" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string BeginClickEffect(string selector) => BeginAttentionPing(selector);

    public static string RemoveAttentionPing() =>
        "(function(){var c=document.getElementById('__agentUpClickRing');if(c)c.remove();return JSON.stringify({ok:true});})()";

    public static string CompleteClick(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"var c=document.getElementById('__agentUpClickRing');" +
        $"if(!e){{if(c)c.remove();return JSON.stringify({{error:'Element not found: '+{Js(selector)}}});}}" +
        $"if(e.matches(':disabled')){{if(c)c.remove();return JSON.stringify({{error:'Element is disabled: '+{Js(selector)}}});}}" +
        $"var r=e.getBoundingClientRect();" +
        $"var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);" +
        $"window.__agentUpMouse={{x:x,y:y}};" +
        $"try{{e.click();return JSON.stringify({{ok:true}});}}" +
        $"catch(ex){{return JSON.stringify({{error:'Click failed: '+(ex&&ex.message?ex.message:ex)}});}}" +
        $"finally{{if(c)c.remove();}}" +
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
