using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Real scene cameras and the existing HUD Canvas; no video or external assets.
public enum MazeStoryBeat { NewGame, AreaComplete, CampaignComplete, BossDefeat, HealthDefeat }
public enum PlayerStoryPose { Neutral, Fear, Relief, Exhausted }

public sealed partial class NewGameCinematic : MonoBehaviour
{
    public MazeStoryBeat Kind { get; private set; }
    public bool IsActive { get; private set; }
    public int Shot { get; private set; }
    public float ElapsedSeconds => elapsed;
#if UNITY_EDITOR
    // Native editor verification can render the sequence without stealing desktop focus.
    public bool PreviewWhileUnfocused { get; set; }
#endif
    MazeBetaGame game;
    Camera view;
    PlayerFollowCamera follow;
    PlayerLimbAnimator actor;
    GameObject overlay, stats;
    Image fade;
    Text heading, title, subtitle;
    readonly List<Renderer> labels = new List<Renderer>();
    readonly List<bool> labelStates = new List<bool>();
    bool statsWasActive, followWasEnabled, closing, restoredCamera;
    float elapsed, duration, exitTime, savedFov, savedFar;
    Vector3 savedPosition;
    Quaternion savedRotation;
    float shotOrbit;
    bool orbitChosen;

    public void Play(MazeBetaGame owner, Camera camera, PlayerFollowCamera controller, float seconds, MazeStoryBeat kind = MazeStoryBeat.NewGame)
    {
        if (IsActive) Cancel();
        game=owner;view=camera;follow=controller;duration=Mathf.Clamp(seconds,5,20);Kind=kind;
        elapsed=exitTime=0;closing=restoredCamera=false;Shot=-1;
        savedPosition=view.transform.position;savedRotation=view.transform.rotation;
        savedFov=view.fieldOfView;savedFar=view.farClipPlane;
        followWasEnabled=follow && follow.enabled;if(follow)follow.enabled=false;
        actor=game.Player.GetComponent<PlayerLimbAnimator>();if(actor)actor.CinematicIdle=true;
        PrepareStory();
        var hud=FindAnyObjectByType<MazeCanvasHUD>();
        if (!hud) { Restore(); return; }
        stats=hud.transform.Find("Stats")?.gameObject;statsWasActive=stats && stats.activeSelf;
        if(stats)stats.SetActive(false);
        foreach(var label in game.GetComponentsInChildren<TextMesh>())
        { var renderer=label.GetComponent<Renderer>();if(!renderer)continue;labels.Add(renderer);labelStates.Add(renderer.enabled);renderer.enabled=false; }
        BuildOverlay(hud.transform);
        IsActive=true;enabled=true;
        MazeAudioDirector.BeginStory(Kind);
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        EvaluateShot(0);
    }
    void Update()
    {
        if(!IsActive)return;
        // Do not silently consume a cinematic while the player is in another app.
        if(!Application.isFocused
#if UNITY_EDITOR
            && !PreviewWhileUnfocused
#endif
        )return;
        float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);elapsed+=dt;
        var keys=Keyboard.current;var pad=Gamepad.current;
        if(elapsed>.35f && ((keys!=null && (keys.spaceKey.wasPressedThisFrame||keys.escapeKey.wasPressedThisFrame||keys.enterKey.wasPressedThisFrame))
            || (pad!=null && (pad.buttonSouth.wasPressedThisFrame||pad.startButton.wasPressedThisFrame)))) Skip();
        if(!closing && elapsed>=duration-1.1f)Skip();
        if(closing)
        {
            exitTime+=dt;
            if(exitTime>=.45f && !restoredCamera){RestoreCamera();restoredCamera=true;}
            fade.color=new Color(0,0,0,exitTime<.45f?Mathf.SmoothStep(0,1,exitTime/.45f):1-Mathf.SmoothStep(0,1,(exitTime-.45f)/.65f));
            if(exitTime>=1.1f)Restore();
            return;
        }
        float normalized=Mathf.Clamp01(elapsed/(duration-1.1f));
        EvaluateShot(normalized);
        float alpha=1-Mathf.SmoothStep(0,1,elapsed/.65f);
        foreach(float cut in new[]{.36f,.69f})
            alpha=Mathf.Max(alpha,1-Mathf.Clamp01(Mathf.Abs(normalized-cut)/.018f));
        fade.color=new Color(0,0,0,alpha);
    }
    void EvaluateShot(float t)
    {
        int shot=t<.36f?0:t<.69f?1:2;
        if(Shot!=shot)
        {
            Shot=shot;
            MazeAudioDirector.StoryShot(Kind,shot);
            orbitChosen=false;
            SetStoryText(shot);
        }
        AnimateStory(t,shot);
        Vector3 target,position;
        float fov;
        if(UsesOverview(shot))
        {
            float u=Mathf.SmoothStep(0,1,t/.36f);
            target=StoryOverviewTarget()+Vector3.up*game.wallHeight*.6f;
            position=target+Vector3.Lerp(new Vector3(-game.cellSize*1.5f,game.wallHeight+5,-game.cellSize*1.8f),new Vector3(-game.cellSize,game.wallHeight+2.5f,-game.cellSize*1.3f),u);
            position.y=Mathf.Max(position.y,game.wallHeight+2);fov=55;
        }
        else
        {
            float u=Mathf.SmoothStep(0,1,shot==1?(t-.36f)/.33f:(t-.69f)/.31f);
            Transform subject=PaperShot(shot)&&storyPaper?storyPaper.transform:game.Player.visual;
            bool paper=subject!=game.Player.visual;
            target=paper?subject.TransformPoint(Vector3.up*.98f):subject.position+Vector3.up*1.33f;
            Vector3 front=game.Player.visual.forward;
            Vector3 offset=front*Mathf.Lerp(paper?1.55f:1.45f,paper?1.35f:1.25f,u)
                +game.Player.visual.right*Mathf.Lerp(.45f,-.18f,u)+Vector3.up*.04f;
            if(actor && actor.HasOutcomeMotion && !paper)
            {
                target=subject.position+Vector3.up*1.05f;
                offset=front*2.35f+game.Player.visual.right*.35f+Vector3.up*.25f;
            }
            position=SafePosition(target,target+offset,subject);fov=paper?67:55;
        }
        view.farClipPlane=Mathf.Max(savedFar,game.cellSize*20);
        view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
        view.fieldOfView=fov;
        FaceStoryPaper();
    }
    Vector3 SafePosition(Vector3 target,Vector3 desired,Transform subject)
    {
        Vector3 original=desired-target;
        if(!orbitChosen)
        {
            float best=float.NegativeInfinity;shotOrbit=0;
            foreach(float angle in new[]{0f,35,-35,70,-70,110,-110,150,-150,180})
            {
                Vector3 candidate=Quaternion.Euler(0,angle,0)*original;
                float available=AvailableDistance(target,candidate,subject);
                Vector3 point=target+candidate.normalized*available;
                if(subject!=game.Player.visual && Vector3.ProjectOnPlane(point-game.Player.visual.position,Vector3.up).sqrMagnitude<.35f)continue;
                float score=available-Mathf.Abs(angle)*.0015f;
                if(score>best){best=score;shotOrbit=angle;}
                if(angle==0 && available>=original.magnitude*.93f)break;
            }
            orbitChosen=true;
        }
        Vector3 delta=Quaternion.Euler(0,shotOrbit,0)*original;
        float distance=AvailableDistance(target,delta,subject);
        if(distance<.85f)
        {
            // A very tight corner needs a cutaway wall rather than a camera inside the actor.
            foreach(var hit in Physics.SphereCastAll(target,.08f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
            {
                if(IgnoreCameraCollider(hit.collider,subject))continue;
                foreach(var renderer in hit.collider.GetComponentsInChildren<Renderer>())
                    if(!labels.Contains(renderer)){labels.Add(renderer);labelStates.Add(renderer.enabled);renderer.enabled=false;}
            }
            distance=delta.magnitude;
        }
        return target+delta.normalized*distance;
    }
    float AvailableDistance(Vector3 target,Vector3 delta,Transform subject)
    {
        float distance=delta.magnitude;
        foreach(var hit in Physics.SphereCastAll(target,.08f,delta.normalized,distance,~0,QueryTriggerInteraction.Ignore))
            if(!IgnoreCameraCollider(hit.collider,subject))distance=Mathf.Min(distance,Mathf.Max(.25f,hit.distance-.12f));
        return distance;
    }
    bool IgnoreCameraCollider(Collider collider,Transform subject) => collider.transform.IsChildOf(subject)
        ||collider.transform.IsChildOf(game.Player.transform)||collider.transform.IsChildOf(game.Boss.transform);
    public void Skip() { if(IsActive && !closing){closing=true;exitTime=0;} }
    public void Cancel() { if(IsActive)Restore(); }
    void RestoreCamera()
    {
        if(view){view.transform.SetPositionAndRotation(savedPosition,savedRotation);view.fieldOfView=savedFov;view.farClipPlane=savedFar;}
    }
    void Restore()
    {
        IsActive=false;RestoreCamera();
        MazeAudioDirector.EndStory();
        RestoreStory();
        if(actor){actor.CinematicIdle=false;actor.StoryPose=PlayerStoryPose.Neutral;actor.StoryPoseWeight=0;}
        for(int i=0;i<labels.Count;i++)if(labels[i])labels[i].enabled=labelStates[i];labels.Clear();labelStates.Clear();
        if(stats)stats.SetActive(statsWasActive);
        if(overlay){overlay.SetActive(false);Destroy(overlay);}overlay=null;
        if(follow)follow.enabled=followWasEnabled;
        if(game)game.IntroFinished();
    }
    void OnDisable(){if(IsActive)Restore();}
    void BuildOverlay(Transform canvas)
    {
        var root=Rect("Nueva partida - cinematica",canvas,Vector2.zero,Vector2.one);
        overlay=root.gameObject;var blocker=overlay.AddComponent<Image>();blocker.color=Color.clear;blocker.raycastTarget=true;
        var top=Rect("Banda superior",root,new Vector2(0,.84f),Vector2.one);Panel(top,new Color(.018f,.025f,.024f,.97f));
        var bottom=Rect("Banda inferior",root,Vector2.zero,new Vector2(1,.20f));Panel(bottom,new Color(.018f,.025f,.024f,.97f));
        heading=Label("Expediente",top,"",19,new Vector2(64,-28),new Vector2(1300,40),new Color(.82f,.77f,.62f));
        title=Label("Titulo",bottom,"",45,new Vector2(64,-20),new Vector2(1650,60),new Color(.96f,.92f,.82f));title.fontStyle=FontStyle.Bold;
        subtitle=Label("Subtitulo",bottom,"",25,new Vector2(66,-91),new Vector2(1650,70),new Color(.78f,.79f,.73f));
        var skipRect=Rect("Saltar cinematica",top,Vector2.one,Vector2.one);skipRect.pivot=new Vector2(1,1);skipRect.anchoredPosition=new Vector2(-52,-26);skipRect.sizeDelta=new Vector2(395,52);
        var background=skipRect.gameObject.AddComponent<Image>();background.color=new Color(.35f,.32f,.24f,.5f);
        var button=skipRect.gameObject.AddComponent<Button>();button.targetGraphic=background;button.onClick.AddListener(Skip);
        var skipLabel=Label("Texto",skipRect,"SALTAR · ESPACIO / ESC",20,new Vector2(12,-7),new Vector2(370,38),new Color(1,.88f,.58f));skipLabel.alignment=TextAnchor.MiddleCenter;
        var veil=Rect("Fundido",root,Vector2.zero,Vector2.one);fade=veil.gameObject.AddComponent<Image>();fade.color=Color.black;fade.raycastTarget=false;
        MazeAudioDirector.RegisterButtons(root);
    }
    static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.gameObject.layer=5;
        r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return r;
    }
    static void Panel(RectTransform rect,Color color){var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;}
    static Text Label(string name,Transform parent,string text,int size,Vector2 position,Vector2 dimensions,Color color)
    {
        var r=Rect(name,parent,new Vector2(0,1),new Vector2(0,1));r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=dimensions;
        var label=r.gameObject.AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=size;label.color=color;label.text=text;
        label.alignment=TextAnchor.UpperLeft;label.raycastTarget=false;return label;
    }
}
