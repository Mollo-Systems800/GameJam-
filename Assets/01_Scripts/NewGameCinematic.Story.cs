using UnityEngine;

// Staged visuals never move the gameplay controller, change health or consume run time.
public sealed partial class NewGameCinematic
{
    GameObject storyPaper;
    Vector3 visualPosition;
    Quaternion visualRotation;
    void PrepareStory()
    {
        if(actor) actor.PlayOutcomeMotion(game.HasWon, game.IsFinalDefeat);
        visualPosition=game.Player.visual.localPosition;visualRotation=game.Player.visual.localRotation;
        if(Kind==MazeStoryBeat.HealthDefeat && game.Player.transform.position.y<-.1f)
        {
            int cell=Mathf.Clamp(game.WorldCell(game.Player.transform.position),0,game.Grid.Count-1);
            game.Player.visual.position=game.CellPosition(cell)+Vector3.up*.05f;
        }
        if((Kind==MazeStoryBeat.NewGame||Kind==MazeStoryBeat.BossDefeat||Kind==MazeStoryBeat.CampaignComplete)&&game.bossVisualPrefab)
        {
            storyPaper=Instantiate(game.bossVisualPrefab);
            storyPaper.name="Solicitud - utileria cinematica";
            foreach(var behaviour in storyPaper.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
            foreach(var collider in storyPaper.GetComponentsInChildren<Collider>())collider.enabled=false;
        }
    }
    bool UsesOverview(int shot) => game.IsFinalDefeat || game.HasWon ? false : Kind==MazeStoryBeat.NewGame?shot==0:
        Kind==MazeStoryBeat.AreaComplete?shot==0||shot==2:
        Kind==MazeStoryBeat.CampaignComplete?shot==2:Kind==MazeStoryBeat.HealthDefeat&&shot==2;
    bool PaperShot(int shot) => game.IsFinalDefeat ? false : Kind==MazeStoryBeat.NewGame?shot==2:
        Kind==MazeStoryBeat.BossDefeat?shot!=0:Kind==MazeStoryBeat.CampaignComplete&&shot==1;
    Vector3 StoryOverviewTarget()
    {
        if(Kind==MazeStoryBeat.NewGame)return game.CellPosition(0)+new Vector3(game.cellSize,0,game.cellSize);
        return game.Player.visual.position;
    }
    void AnimateStory(float t,int shot)
    {
        if(actor && !actor.HasOutcomeMotion)
        {
            actor.StoryPose=Kind==MazeStoryBeat.HealthDefeat?PlayerStoryPose.Exhausted:
                Kind==MazeStoryBeat.NewGame||Kind==MazeStoryBeat.BossDefeat?PlayerStoryPose.Fear:PlayerStoryPose.Relief;
            actor.StoryPoseWeight=Mathf.SmoothStep(0,1,Mathf.Clamp01(t*4));
        }
        if(!storyPaper)return;
        storyPaper.SetActive(PaperShot(shot));
        Vector3 forward=game.Player.visual.forward;
        Vector3 origin=game.Player.visual.position+forward*Mathf.Lerp(1.65f,1.1f,t);
        Quaternion facing=Quaternion.LookRotation(forward);
        if(Kind==MazeStoryBeat.CampaignComplete)
        {
            float drop=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.36f,.68f,t));
            origin.y+=.6f*(1-drop);
            facing*=Quaternion.Euler(-85*drop,0,12*drop);
        }
        else origin.y+=.035f*Mathf.Sin(elapsed*3);
        storyPaper.transform.SetPositionAndRotation(origin,facing);
    }
    void RestoreStory()
    {
        if(actor) actor.StopOutcomeMotion();
        if(game&&game.Player&&game.Player.visual)
        { game.Player.visual.localPosition=visualPosition;game.Player.visual.localRotation=visualRotation; }
        if(storyPaper){storyPaper.SetActive(false);Destroy(storyPaper);}storyPaper=null;
    }
    void FaceStoryPaper()
    {
        if(!storyPaper||!storyPaper.activeSelf)return;
        Vector3 direction=view.transform.position-storyPaper.transform.position;direction.y=0;
        if(direction.sqrMagnitude<.001f)return;
        float drop=Kind==MazeStoryBeat.CampaignComplete?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.36f,.68f,elapsed/(duration-1.1f))):0;
        storyPaper.transform.rotation=Quaternion.LookRotation(direction)*Quaternion.Euler(-85*drop,0,12*drop);
    }
    void SetStoryText(int shot)
    {
        string area=game.ActiveArea.areaName;
        if(game.IsFinalDefeat)
        {
            heading.text="SIN VIDAS / CONTRATACIÓN DEFINITIVA";
            title.text="TE TOCÓ FIRMAR.";
            subtitle.text="La fuga terminó. Volverás al nivel 1 con tres vidas.";
            return;
        }
        if(game.HasWon && !game.CampaignComplete && game.CurrentLevelInArea != 2)
        {
            heading.text=$"{area.ToUpperInvariant()} / NIVEL {game.CurrentLevelIndex+1}";
            title.text="¡NIVEL SUPERADO!";
            subtitle.text=$"Un trámite menos. Conservas {game.Progress.Health} vidas para seguir escapando.";
            return;
        }
        string[] titles,lines;
        switch(Kind)
        {
            case MazeStoryBeat.AreaComplete:
                heading.text=$"EXPEDIENTE EN FUGA  /  ÁREA {game.CurrentAreaIndex+1} DE 3";
                titles=new[]{area.ToUpperInvariant()+": SUPERADO.","UN RESPIRO. NADA MÁS.","OTRO DEPARTAMENTO TE ESPERA."};
                lines=new[]{"Tres laberintos menos. Sigues sin firmar.","Creí que aquí se acababan los trámites…",$"Próxima parada: {(game.areas!=null&&game.CurrentAreaIndex+1<game.areas.Length?game.areas[game.CurrentAreaIndex+1].areaName:"la salida")}. La solicitud no se rinde."};break;
            case MazeStoryBeat.CampaignComplete:
                heading.text="EXPEDIENTE CERRADO  /  3 ÁREAS · 9 NIVELES";
                titles=new[]{"¡POR FIN, FUERA!","HOY NO FIRMO.","LIBRE. Y SIN VACANTES."};
                lines=new[]{"Nueve laberintos. Ni una entrevista.","La solicitud pierde altura. Tú recuperas el aliento.",$"Tiempo de fuga: {CampaignRanking.Format(game.CampaignSeconds)}. Guarda tu nombre en el ranking."};break;
            case MazeStoryBeat.BossDefeat:
                heading.text="RECURSOS HUMANOS  /  CAPTURA DEL POSTULANTE";
                titles=new[]{"NO. ESE PAPEL NO.","LA SOLICITUD TE ENCONTRÓ.","CONTRATACIÓN FORZOSA."};
                lines=new[]{"Escapar del trabajo parecía una buena idea.","Nombre, experiencia… y una firma que nunca quisiste dar.","No firmes todavía. Habrá otro intento."};break;
            case MazeStoryBeat.HealthDefeat:
                heading.text=$"BAJA DEL POSTULANTE  /  {area.ToUpperInvariant()} · NIVEL {game.CurrentLevelInArea+1}";
                titles=new[]{"SE ACABÓ EL ALIENTO.","NECESITO UN DESCANSO.","LA FUGA QUEDA PENDIENTE."};
                lines=new[]{game.DefeatReason+".","Esta vez fueron los golpes y los obstáculos. No la solicitud.","Respira. El próximo intento está por empezar."};break;
            default:
                heading.text=shot==0?"OFICINA DE EMPLEO  /  UN LUNES CUALQUIERA":shot==1?"ESTADO DEL POSTULANTE: QUIERE HUIR":"LA SOLICITUD HA COBRADO VIDA";
                titles=new[]{"¿TRABAJAR? PASO.","NO PIENSO RELLENAR ESO.","¡QUE ALGUIEN ME SAQUE DE AQUÍ!"};
                lines=new[]{"Horarios. Entrevistas. Otra vez lo mismo. Esto no es para mí.","Una solicitud de empleo. Su peor pesadilla tiene casillas y pide experiencia.","El papel viene a buscarte. Escapa de las tres áreas antes de que te haga firmar."};break;
        }
        title.text=titles[shot];subtitle.text=lines[shot];
    }
}
