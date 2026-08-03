using System.Text.Json;

namespace AgentUp.Desktop.Features.Browser.Resources;

internal static class BrowserScripts
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
        $"m.style.cssText='position:fixed;left:'+window.__agentUpMouse.x+'px;top:'+window.__agentUpMouse.y+'px;width:14px;height:14px;margin:-7px 0 0 -7px;border-radius:999px;background:#14d86f;box-shadow:0 0 0 2px rgba(0,0,0,.45),0 0 18px rgba(20,216,111,.7);z-index:2147483647;pointer-events:none;transition:left 200ms linear,top 200ms linear;';" +
        $"requestAnimationFrame(function(){{m.style.left=x+'px';m.style.top=y+'px';}});" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

    public static string BeginClickEffect(string selector) =>
        $"(function(){{" +
        $"var e=document.querySelector({Js(selector)});" +
        $"if(!e)return JSON.stringify({{error:'Element not found: '+{Js(selector)}}});" +
        $"e.scrollIntoView({{block:'center',inline:'center'}});" +
        $"var r=e.getBoundingClientRect();" +
        $"var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);" +
        $"var c=document.getElementById('__agentUpClickRing');" +
        $"if(c)c.remove();" +
        $"c=document.createElement('div');c.id='__agentUpClickRing';document.documentElement.appendChild(c);" +
        $"c.style.cssText='position:fixed;left:'+x+'px;top:'+y+'px;width:74px;height:74px;margin:-37px 0 0 -37px;border:3px solid rgba(20,216,111,.95);border-radius:999px;z-index:2147483646;pointer-events:none;transform:scale(1.35);opacity:.95;transition:transform 200ms ease-in,opacity 200ms ease-in;';" +
        $"requestAnimationFrame(function(){{c.style.transform='scale(.12)';c.style.opacity='.2';}});" +
        $"return JSON.stringify({{ok:true}});" +
        $"}})()";

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
