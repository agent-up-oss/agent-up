using System.Text.Json;
using AgentUp.Browser.Streaming.DTOs;

namespace AgentUp.Browser.Streaming.Resources;

public static class RemoteDisplayViewerPage
{
    public static string Build(RemoteDisplayViewerOptions options)
    {
        var configuration = JsonSerializer.Serialize(new
        {
            title = options.Title,
            socketPath = options.DisplayWebSocketPath,
            mimeType = options.FrameMimeType,
            width = options.Width,
            height = options.Height
        });

        return """
            <!doctype html>
            <html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no">
            <title>__TITLE__</title><style>
            html,body{margin:0;width:100%;height:100%;overflow:hidden;background:#111;color:#eee;font:14px system-ui}
            #display{display:block;width:100%;height:100%;object-fit:contain;touch-action:none;outline:none}
            #status{position:fixed;left:12px;top:12px;padding:6px 10px;border-radius:6px;background:#111d;pointer-events:none}
            </style></head><body><canvas id="display" tabindex="0"></canvas><div id="status">Connecting…</div>
            <script>
            (()=>{'use strict';const cfg=__CONFIG__,canvas=document.getElementById('display'),status=document.getElementById('status'),ctx=canvas.getContext('2d');
            canvas.width=cfg.width;canvas.height=cfg.height;let socket=null,retry=250,closed=false,frameUrl=null;
            const send=value=>{if(socket?.readyState===WebSocket.OPEN)socket.send(JSON.stringify(value));};
            const point=e=>{const r=canvas.getBoundingClientRect(),scale=Math.min(r.width/canvas.width,r.height/canvas.height),left=r.left+(r.width-canvas.width*scale)/2,top=r.top+(r.height-canvas.height*scale)/2;return{x:Math.max(0,Math.min(canvas.width-1,Math.round((e.clientX-left)/scale))),y:Math.max(0,Math.min(canvas.height-1,Math.round((e.clientY-top)/scale)))}};
            const connect=()=>{if(closed)return;const scheme=location.protocol==='https:'?'wss:':'ws:';socket=new WebSocket(scheme+'//'+location.host+cfg.socketPath);socket.binaryType='blob';
              socket.onopen=()=>{retry=250;status.textContent='Waiting for desktop…';send({type:'presence',state:document.hidden?'background':'foreground'});};
              socket.onmessage=e=>{if(typeof e.data==='string')return;const url=URL.createObjectURL(e.data.slice(0,e.data.size,cfg.mimeType)),image=new Image();image.onload=()=>{if(frameUrl)URL.revokeObjectURL(frameUrl);frameUrl=url;canvas.width=image.width;canvas.height=image.height;ctx.drawImage(image,0,0);status.hidden=true;};image.onerror=()=>URL.revokeObjectURL(url);image.src=url;};
              socket.onclose=()=>{status.hidden=false;status.textContent='Reconnecting…';setTimeout(connect,retry);retry=Math.min(4000,retry*2);};};
            canvas.addEventListener('pointerdown',e=>{canvas.focus();canvas.setPointerCapture(e.pointerId);send({type:'pointerDown',button:e.button,...point(e)});});
            canvas.addEventListener('pointermove',e=>send({type:'pointerMove',...point(e)}));
            canvas.addEventListener('pointerup',e=>send({type:'pointerUp',button:e.button,...point(e)}));
            canvas.addEventListener('wheel',e=>{e.preventDefault();send({type:'wheel',deltaX:e.deltaX,deltaY:e.deltaY});},{passive:false});
            canvas.addEventListener('keydown',e=>{e.preventDefault();send({type:'keyDown',key:e.key});});canvas.addEventListener('keyup',e=>send({type:'keyUp',key:e.key}));
            document.addEventListener('visibilitychange',()=>send({type:'presence',state:document.hidden?'background':'foreground'}));window.addEventListener('pagehide',()=>{closed=true;socket?.close();});connect();})();
            </script></body></html>
            """
            .Replace("__TITLE__", Encode(options.Title), StringComparison.Ordinal)
            .Replace("__CONFIG__", configuration, StringComparison.Ordinal);
    }

    private static string Encode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
