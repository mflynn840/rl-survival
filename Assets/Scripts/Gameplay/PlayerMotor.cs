using UnityEngine;

namespace RLSurvival
{
    public sealed class PlayerMotor : MonoBehaviour
    {
        SurvivalGame game;
        public void Initialize(SurvivalGame owner){game=owner;}
        public void Tick(float deltaTime)
        {
            if(game==null||game.Arena.LocalPlayer==null)return;
            float speed=game.HasPerk(0)?7.5f:5.8f;
            Vector3 direction=game.Arena.MoveDirection(new Vector3(Input.GetAxisRaw("Horizontal"),0,-Input.GetAxisRaw("Vertical")));if(direction.sqrMagnitude>1)direction.Normalize();
            var player=game.Arena.LocalPlayer;player.transform.position=Vector3.MoveTowards(player.transform.position,player.transform.position+direction*speed*deltaTime,speed*deltaTime);
            player.transform.position=new Vector3(Mathf.Clamp(player.transform.position.x,-13.5f,13.5f),0,Mathf.Clamp(player.transform.position.z,4f,24.5f));
            game.Arena.AnimateLocal(direction,speed);
            if(game.IsHost)game.SetPlayerPosition(game.LocalPlayerId,player.transform.position);
        }
    }
}
