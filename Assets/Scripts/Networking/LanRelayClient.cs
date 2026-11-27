using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RLSurvival
{
    public sealed class LanRelayClient : MonoBehaviour
    {
        SurvivalGame game;
        int lastSequence;
        float pollAt;
        bool pollPending;
        public bool Connected { get; private set; }
        public bool IsHost { get; private set; }
        public string ClientId { get; private set; }
        public string HostId { get; private set; }
        public RoomPlayer[] Players { get; private set; }
        public void Initialize(SurvivalGame owner){game=owner;Players=new RoomPlayer[0];}
        public void Join(string room){if(!Connected)StartCoroutine(JoinRoutine(room));}
        public void Leave(){if(Connected){StartCoroutine(LeaveRoutine(ClientId));Connected=false;IsHost=false;ClientId="";HostId="";pollPending=false;Players=new RoomPlayer[0];}}
        public void Send(string type,string payload){if(Connected)StartCoroutine(SendRoutine(type,payload));}
        public void SendMove(Vector3 position){Send("move",JsonUtility.ToJson(new GameCommand{action="move",x=position.x,z=position.z}));}
        public void UpdateHost(string id){HostId=id;IsHost=HostId==ClientId;}
        void Update(){if(Connected&&!pollPending&&Time.unscaledTime>=pollAt){pollAt=Time.unscaledTime+.2f;StartCoroutine(PollRoutine());}}

        IEnumerator JoinRoutine(string room)
        {
            game.Status="Connecting to "+game.ServerUrl;
            var data=JsonUtility.ToJson(new JoinRequest{token=game.Account.Token,room=room});
            using(var request=JsonRequest(game.ServerUrl+"/join",data))
            {
                request.timeout=8;yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){game.Status="Join failed: "+request.downloadHandler.text;yield break;}
                var welcome=JsonUtility.FromJson<RelayEvent>(request.downloadHandler.text);ClientId=welcome.from;lastSequence=welcome.seq;Players=welcome.players??new RoomPlayer[0];HostId=welcome.payload;IsHost=HostId==ClientId;Connected=true;
                game.OnRoomJoined(room,IsHost);
            }
        }
        IEnumerator PollRoutine()
        {
            pollPending=true;
            using(var request=UnityWebRequest.Get(game.ServerUrl+"/poll?id="+UnityWebRequest.EscapeURL(ClientId)+"&since="+lastSequence))
            {
                request.timeout=4;yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success&&Connected)
                {
                    var lines=request.downloadHandler.text.Split('\n');
                    foreach(string line in lines){if(string.IsNullOrWhiteSpace(line))continue;RelayEvent data=null;try{data=JsonUtility.FromJson<RelayEvent>(line);}catch{}if(data==null||data.seq<=lastSequence)continue;lastSequence=data.seq;HandleEvent(data);}
                }
                else if(Connected)game.Status="Relay poll failed: "+request.error;
            }
            pollPending=false;
        }
        void HandleEvent(RelayEvent data)
        {
            if(data.type=="host"){UpdateHost(data.from);game.OnHostChanged(IsHost);}
            else if(data.type=="leave"){game.OnPlayerLeft(data.from);}
            else if(data.type=="join"){game.OnPlayerJoined(data);}
            else if(data.type=="move"){game.OnPlayerMoved(data.from,data.payload);}
            else if(data.type=="start"&&data.from!=ClientId){var options=JsonUtility.FromJson<MatchOptions>(data.payload);game.StartMatchFromHost(options);}
            else if(data.type=="lobby")game.ReturnToLobbyFromHost();
            else if(data.type=="state"){var snapshot=JsonUtility.FromJson<MatchSnapshot>(data.payload);if(snapshot!=null)game.ApplySnapshot(snapshot);}
            else if(data.type=="action")game.ApplyHostAction(data.payload);
            else if(data.type=="model"&&IsHost)game.OnAgentModelReceived(data.payload);
            else if(data.type=="input"&&IsHost)game.ApplyClientInput(data.from,data.payload);
        }
        public void AddPlayer(string id,string name)
        {
            foreach(var player in Players)if(player.id==id)return;var updated=new RoomPlayer[Players.Length+1];Players.CopyTo(updated,0);updated[updated.Length-1]=new RoomPlayer{id=id,name=name};Players=updated;
        }
        public void RemovePlayer(string id){var updated=new System.Collections.Generic.List<RoomPlayer>();foreach(var player in Players)if(player.id!=id)updated.Add(player);Players=updated.ToArray();}
        IEnumerator SendRoutine(string type,string payload)
        {
            string body="{\"id\":\""+ClientId+"\",\"type\":\""+type+"\",\"payload\":\""+Escape(payload)+"\"}";
            using(var request=JsonRequest(game.ServerUrl+"/send",body)){request.timeout=5;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)game.Status="Relay send failed: "+request.error;}
        }
        IEnumerator LeaveRoutine(string id){using(var request=UnityWebRequest.Get(game.ServerUrl+"/leave?id="+UnityWebRequest.EscapeURL(id)))yield return request.SendWebRequest();}
        static UnityWebRequest JsonRequest(string url,string body)
        {
            var request=new UnityWebRequest(url,"POST");request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");return request;
        }
        static string Escape(string value){return value.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n");}
        void OnDestroy(){if(Connected)Leave();}
    }
}
