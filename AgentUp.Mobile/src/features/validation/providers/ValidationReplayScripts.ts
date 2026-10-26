import type { ValidationTarget } from '../models/ValidationFlow';

export const ReplayAnimationMs = 500;
const AccentBright = '#00d66b';
const Canvas = '#0a0b0c';

const FindElement = `
function find(target){
  target=target||{};
  if(target.selector)return document.querySelector(target.selector);
  var nodes=Array.from(document.querySelectorAll('a,button,input,select,textarea,[role],[data-testid],[data-test-id]'));
  return nodes.find(function(e){
    if(e.offsetParent===null&&e!==document.body)return false;
    if(target.testId){
      var id=e.getAttribute('data-testid')||e.getAttribute('data-test-id')||e.getAttribute('testID');
      if(id===target.testId)return true;
    }
    var label=(e.getAttribute('aria-label')||e.getAttribute('name')||e.id||'').trim();
    var text=((e.innerText||e.textContent||e.value||'')+'').replace(/\\s+/g,' ').trim();
    if(target.name&&(label===target.name||text===target.name||e.getAttribute('name')===target.name))return true;
    if(target.label&&(label===target.label||text===target.label))return true;
    if(target.text&&text.indexOf(target.text)>=0)return true;
    if(target.role&&((e.getAttribute('role')||'')===target.role||e.tagName.toLowerCase()===target.role))return true;
    return false;
  })||null;
}
`.replace(/\n/g, '');

export function replayTarget(target: ValidationTarget | string | null | undefined): ValidationTarget {
  if (target == null) return {};
  if (typeof target === 'string') return { selector: target };
  return target;
}

export function beginMouseMoveScript(target: ValidationTarget | string): string {
  return `(function(){${FindElement}var t=${js(replayTarget(target))};var e=find(t);if(!e)return JSON.stringify({error:'Element not found'});e.scrollIntoView({block:'center',inline:'center'});var r=e.getBoundingClientRect();var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);if(!window.__agentUpMouse)window.__agentUpMouse={x:Math.round(window.innerWidth/2),y:Math.round(window.innerHeight/2)};var m=document.getElementById('__agentUpMouse');if(!m){m=document.createElement('div');m.id='__agentUpMouse';document.documentElement.appendChild(m);}m.style.cssText='position:fixed;left:'+window.__agentUpMouse.x+'px;top:'+window.__agentUpMouse.y+'px;width:14px;height:14px;margin:-7px 0 0 -7px;border-radius:999px;background:${AccentBright};box-shadow:0 0 0 2px color-mix(in srgb,${Canvas} 45%,transparent),0 0 18px color-mix(in srgb,${AccentBright} 70%,transparent);z-index:2147483647;pointer-events:none;transition:left ${ReplayAnimationMs}ms linear,top ${ReplayAnimationMs}ms linear;';requestAnimationFrame(function(){m.style.left=x+'px';m.style.top=y+'px';window.__agentUpMouse={x:x,y:y};});return JSON.stringify({ok:true});})()`;
}

export function beginAttentionPingScript(target: ValidationTarget | string): string {
  return `(function(){${FindElement}var t=${js(replayTarget(target))};var e=find(t);if(!e)return JSON.stringify({error:'Element not found'});e.scrollIntoView({block:'center',inline:'center'});var r=e.getBoundingClientRect();var x=Math.round(r.left+r.width/2),y=Math.round(r.top+r.height/2);var c=document.getElementById('__agentUpClickRing');if(c)c.remove();c=document.createElement('div');c.id='__agentUpClickRing';document.documentElement.appendChild(c);c.style.cssText='position:fixed;left:'+x+'px;top:'+y+'px;width:64px;height:64px;margin:-32px 0 0 -32px;border:3px solid ${AccentBright};border-radius:999px;z-index:2147483646;pointer-events:none;transform:scale(.35);opacity:.95;transition:transform ${ReplayAnimationMs}ms ease-out,opacity ${ReplayAnimationMs}ms ease-out;';requestAnimationFrame(function(){c.style.transform='scale(1.8)';c.style.opacity='0';});return JSON.stringify({ok:true});})()`;
}

export function completeClickScript(target: ValidationTarget | string): string {
  return `(function(){${FindElement}var t=${js(replayTarget(target))};var e=find(t);var c=document.getElementById('__agentUpClickRing');if(!e){if(c)c.remove();return JSON.stringify({error:'Element not found'});}if(e.matches(':disabled')){if(c)c.remove();return JSON.stringify({error:'Element is disabled'});}var r=e.getBoundingClientRect();window.__agentUpMouse={x:Math.round(r.left+r.width/2),y:Math.round(r.top+r.height/2)};try{e.click();return JSON.stringify({ok:true});}catch(ex){return JSON.stringify({error:'Click failed: '+(ex&&ex.message?ex.message:ex)});}finally{if(c)c.remove();}})()`;
}

export function fillScript(target: ValidationTarget | string, text: string): string {
  return `(function(){${FindElement}var t=${js(replayTarget(target))};var e=find(t);if(!e)return JSON.stringify({error:'Element not found'});var p=e instanceof HTMLTextAreaElement?HTMLTextAreaElement.prototype:e instanceof HTMLSelectElement?HTMLSelectElement.prototype:HTMLInputElement.prototype;var nv=Object.getOwnPropertyDescriptor(p,'value');if(nv&&nv.set)nv.set.call(e,${js(text)});else e.value=${js(text)};e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));return JSON.stringify({ok:true});})()`;
}

export function pressScript(key: string): string {
  return `(function(){var e=document.activeElement||document.body;['keydown','keypress','keyup'].forEach(function(t){e.dispatchEvent(new KeyboardEvent(t,{key:${js(key)},bubbles:true,cancelable:true}));});return JSON.stringify({ok:true});})()`;
}

export function removeAttentionPingScript(): string {
  return "(function(){var c=document.getElementById('__agentUpClickRing');if(c)c.remove();return JSON.stringify({ok:true});})()";
}

export function checkNavigationScript(): string {
  return 'document.readyState';
}

export function getTitleScript(): string {
  return 'document.title';
}

export function getUrlScript(): string {
  return 'window.location.href';
}

export function checkTextScript(text: string): string {
  return `(document.body.innerText||'').includes(${js(text)})`;
}

export function checkVisibleScript(target: ValidationTarget | string): string {
  return `(function(){${FindElement}var e=find(${js(replayTarget(target))});return !!(e&&e.offsetParent!==null);})()`;
}

function js(value: unknown): string {
  return JSON.stringify(value)
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029');
}
