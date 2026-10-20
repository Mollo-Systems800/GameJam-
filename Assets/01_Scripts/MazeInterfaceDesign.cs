using UnityEngine;
using UnityEngine.UI;

// Presentation only: existing button callbacks, save data and game state remain authoritative.
public static class MazeInterfaceDesign
{
    public static readonly Color Ink=new Color(.075f,.16f,.145f), Paper=new Color(.96f,.94f,.875f);
    public static readonly Color Muted=new Color(.37f,.43f,.39f), Red=new Color(.72f,.24f,.17f), Gold=new Color(.82f,.64f,.30f);
    static readonly Color Line=new Color(.075f,.16f,.145f,.16f);

    public static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.gameObject.layer=5;Place(rect,position,size);return rect;
    }
    public static void Place(RectTransform rect,Vector2 position,Vector2 size)
    {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=size;}
    static Image Box(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var image=Rect(name,parent,position,size).gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
    }
    static Text Label(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,bool bold=false)
    {
        var text=Rect(name,parent,position,size).gameObject.AddComponent<Text>();
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;Typography(text,fontSize,color,bold);return text;
    }
    static void Typography(Text text,int size,Color color,bool bold=false)
    {
        text.fontSize=size;text.color=color;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
        text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;text.resizeTextForBestFit=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        foreach(var shadow in text.GetComponents<Shadow>())shadow.enabled=false;
    }
    public static void Button(Button button,bool primary=false)
    {
        if(!button)return;
        var old=button.GetComponent<StartupPaperButton>();if(old)old.enabled=false;
        var image=button.GetComponent<Image>();if(!image)image=button.gameObject.AddComponent<Image>();
        image.color=Color.white;button.targetGraphic=image;
        button.transition=Selectable.Transition.ColorTint;
        var colors=button.colors;colors.normalColor=primary?Ink:new Color(.86f,.88f,.81f);
        colors.highlightedColor=primary?new Color(.14f,.30f,.25f):new Color(.78f,.83f,.74f);
        colors.selectedColor=colors.highlightedColor;colors.pressedColor=Gold;
        colors.disabledColor=new Color(.64f,.66f,.60f);colors.fadeDuration=.12f;colors.colorMultiplier=1;button.colors=colors;
        var outline=button.GetComponent<Outline>();if(!outline)outline=button.gameObject.AddComponent<Outline>();
        outline.effectColor=primary?new Color(.76f,.61f,.32f,.5f):Line;outline.effectDistance=new Vector2(1,-1);
        foreach(var text in button.GetComponentsInChildren<Text>(true))Typography(text,text.fontSize,primary?Paper:Ink,true);
        if(!button.GetComponent<MazeInterfaceButton>())button.gameObject.AddComponent<MazeInterfaceButton>();
    }
    static void SimpleButton(Button button,Vector2 position,Vector2 size,bool primary,string value=null)
    {
        Place((RectTransform)button.transform,position,size);Button(button,primary);
        var text=button.GetComponentInChildren<Text>(true);
        if(text){Place(text.rectTransform,new Vector2(20,0),size-new Vector2(40,0));Typography(text,21,primary?Paper:Ink,true);text.alignment=TextAnchor.MiddleCenter;if(value!=null)text.text=value;}
    }
    static void Surface(RectTransform panel,Vector2 size)
    {
        panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;panel.sizeDelta=size;
        var image=panel.GetComponent<Image>();if(!image)image=panel.gameObject.AddComponent<Image>();image.color=Paper;image.raycastTarget=true;
        var shadow=panel.GetComponent<Shadow>();if(!shadow)shadow=panel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.4f);shadow.effectDistance=new Vector2(10,-12);
        Box("Top binding",panel,Vector2.zero,new Vector2(size.x,9),Ink);
        Box("Document rule",panel,new Vector2(40,-68),new Vector2(size.x-80,1),Line);
        Label("Document classification",panel,"OFICINA DE EMPLEO   /   EXPEDIENTE EN FUGA",new Vector2(40,-25),new Vector2(size.x-80,24),14,Muted,true);
        Box("Footer rule",panel,new Vector2(40,-size.y+62),new Vector2(size.x-80,1),Line);
    }
    static void Modal(GameObject panel,Vector2 size)
    {
        var rect=(RectTransform)panel.transform;var parent=rect.parent;int order=rect.GetSiblingIndex();
        var overlay=Rect(panel.name+" · overlay",parent,Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;overlay.SetSiblingIndex(order);
        var dim=overlay.gameObject.AddComponent<Image>();dim.color=new Color(.025f,.06f,.055f,.82f);dim.enabled=panel.activeSelf;
        rect.SetParent(overlay,false);Surface(rect,size);
        var motion=overlay.gameObject.AddComponent<MazeInterfaceMotion>();motion.panel=rect;motion.dim=dim;
    }

    public static void Apply(MazeCanvasHUD hud)
    {
        if(hud.transform.Find("Interface design"))return;
        Rect("Interface design",hud.transform,Vector2.zero,Vector2.zero);
        Modal(hud.resultPanel,new Vector2(720,620));var result=hud.resultPanel.transform;
        Place(hud.resultTitle.rectTransform,new Vector2(48,-91),new Vector2(624,90));Typography(hud.resultTitle,38,Ink,true);
        Place(hud.resultDetails.rectTransform,new Vector2(48,-190),new Vector2(624,158));Typography(hud.resultDetails,22,Muted);
        hud.resultDetails.alignment=TextAnchor.UpperLeft;hud.resultDetails.lineSpacing=1.15f;
        SimpleButton(hud.primaryButton,new Vector2(48,-410),new Vector2(624,62),true);
        if(hud.returnToMenuButton)SimpleButton(hud.returnToMenuButton,new Vector2(48,-488),new Vector2(624,50),false,"Volver al escritorio");
        Label("Result footer",result,"TU EXPEDIENTE SIGUE ABIERTO. RESPIRA.",new Vector2(48,-568),new Vector2(624,28),14,Muted);
        if(hud.audioSettings)SimpleButton(hud.audioSettings.openButton,new Vector2(48,-300),new Vector2(624,50),false,"Ajustar sonido");
        var form=hud.GetComponent<CampaignRecordForm>();
        if(form&&form.form){Place((RectTransform)form.form.transform,new Vector2(131,-300),new Vector2(458,88));Typography(form.message,17,Muted);Button(form.saveButton,true);}

        Modal(hud.shopPanel,new Vector2(1100,640));var shop=hud.shopPanel.transform;
        ((RectTransform)shop.Find("Footer rule")).anchoredPosition=new Vector2(40,-536);
        var title=shop.Find("Title")?.GetComponent<Text>();
        if(title){Place(title.rectTransform,new Vector2(40,-84),new Vector2(650,66));title.text="SUMINISTROS PARA LA FUGA";Typography(title,35,Ink,true);}
        Place(hud.shopBalance.rectTransform,new Vector2(40,-145),new Vector2(1020,32));Typography(hud.shopBalance,18,Muted);
        string[] names={"UN POCO DE PRISA","QUE NO TE ALCANCEN","UN SEGUNDO AIRE"};
        string[] benefits={"VELOCIDAD ×1.5","ESCUDO TEMPORAL","RECUPERA 1 VIDA"};
        string[] details={"Muévete más rápido durante 10 s.\nPara salir de un apuro.","Protección durante 8 s.\nUn respiro entre formularios.","Recupera un corazón perdido.\nTu fuga todavía tiene futuro."};
        string[] symbols={">>","[ + ]","+"};int[] prices={6,8,4};
        for(int i=0;i<hud.purchaseButtons.Length&&i<3;i++)
        {
            var card=Box("Supply card "+i,shop,new Vector2(40+i*350,-193),new Vector2(320,316),new Color(.995f,.985f,.95f)).rectTransform;
            Box("Card accent",card,Vector2.zero,new Vector2(320,4),i==1?Gold:Ink);
            Label("Item number",card,"0"+(i+1)+"  /  RECURSO",new Vector2(22,-18),new Vector2(270,24),13,Muted,true);
            var icon=Label("Item symbol",card,symbols[i],new Vector2(22,-51),new Vector2(276,58),44,i==2?Red:Ink,true);
            Label("Item name",card,names[i],new Vector2(22,-115),new Vector2(276,30),20,Ink,true);
            Label("Item benefit",card,benefits[i],new Vector2(22,-151),new Vector2(276,25),15,Red,true);
            Label("Item description",card,details[i],new Vector2(22,-184),new Vector2(276,48),16,Muted);
            var button=hud.purchaseButtons[i];button.transform.SetParent(card,false);
            SimpleButton(button,new Vector2(22,-250),new Vector2(276,48),true,"COMPRAR  /  "+prices[i]+" MONEDAS");
            button.GetComponentInChildren<Text>(true).fontSize=17;
        }
        SimpleButton(hud.closeShopButton,new Vector2(710,-560),new Vector2(350,48),false,"Volver al laberinto  ·  B / Esc");
        Label("Shop footer",shop,"LAS MEJORAS SE ACTIVAN AL COMPRAR.",new Vector2(40,-568),new Vector2(640,30),14,Muted,true);
        Button(hud.shopButton,true);var shopText=hud.shopButton.GetComponentInChildren<Text>();if(shopText)shopText.text="Tienda  [B]";
        if(hud.audioSettings)Audio(hud.audioSettings);
    }

    public static void Apply(StartupMenu menu)
    {
        if(menu.mainPanel.Find("Interface design"))return;
        Rect("Interface design",menu.mainPanel,Vector2.zero,Vector2.zero);
        foreach(var art in menu.menuCanvas.GetComponentsInChildren<StartupPaperArt>(true))
        {
            if(art.drawing==StartupPaperArt.Drawing.Desk)art.color=new Color(.055f,.12f,.105f);
            if(art.drawing==StartupPaperArt.Drawing.Sheet)art.color=Paper;
        }
        var paper=menu.mainPanel.Find("Application form");
        if(paper)
        {
            Box("Folder spine",paper,Vector2.zero,new Vector2(10,946),Gold);
            var title=paper.Find("Title")?.GetComponent<Text>();if(title)Typography(title,88,Ink,true);
            var note=paper.Find("Note")?.GetComponent<Text>();if(note){note.text="Nueve niveles. Una salida. Ninguna intención de trabajar.";Typography(note,21,Muted);}
            var stamp=paper.Find("Vacancies");if(stamp)stamp.localRotation=Quaternion.Euler(0,0,-8);
        }
        foreach(var button in menu.menuCanvas.GetComponentsInChildren<Button>(true))Button(button,button==menu.newGameButton||button==menu.confirmResetButton||button==menu.closeControlsButton||button==menu.finalistsButton);
        foreach(var button in new[]{menu.newGameButton,menu.loadGameButton,menu.controlsButton,menu.rankingButton})
        {
            if(!button)continue;
            var label=button.transform.Find("Label")?.GetComponent<Text>();if(label)Typography(label,26,button==menu.newGameButton?Paper:Ink,true);
            var sub=button.transform.Find("Subtitle")?.GetComponent<Text>();if(sub)Typography(sub,16,button==menu.newGameButton?new Color(.78f,.84f,.74f):Muted);
        }
        foreach(var dialog in menu.dialogs)
        {
            if(!dialog)continue;
            var shadow=dialog.GetComponent<Shadow>();if(!shadow)shadow=dialog.gameObject.AddComponent<Shadow>();shadow.effectDistance=new Vector2(10,-12);shadow.effectColor=new Color(0,0,0,.35f);
        }
        var advice=menu.controlsPanel.transform.Find("Fine print/Advice")?.GetComponent<Text>();
        if(advice)advice.text="La solicitud acelera con el tiempo.\nCada derrota genera otro laberinto.\nSin vidas, vuelves al nivel 1.";
        if(menu.audioSettings)Audio(menu.audioSettings);
    }
    static void Audio(MazeAudioSettingsPanel panel)
    {
        if(!panel.sheet)return;
        var bg=panel.sheet.GetComponent<Image>();if(bg)bg.color=Paper;
        foreach(var text in panel.sheet.GetComponentsInChildren<Text>(true))text.color=Ink;
        Button(panel.closeButton,true);Button(panel.openButton,false);
        var closeLabel=panel.closeButton.GetComponentInChildren<Text>(true);if(closeLabel)closeLabel.alignment=TextAnchor.MiddleCenter;
        if(!panel.sheet.Find("Audio binding"))Box("Audio binding",panel.sheet,Vector2.zero,new Vector2(panel.sheet.sizeDelta.x,7),Gold);
    }
}
