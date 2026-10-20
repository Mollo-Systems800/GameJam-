using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MazeInterfaceVerification
{
    const string Key="MazeInterfaceVerification.Active";
    static int stage;
    static double next;
    static MazeInterfaceVerification(){EditorApplication.update+=Tick;}
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Capture(string name){ScreenCapture.CaptureScreenshot("Logs/ui-"+name+".png");}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!EditorApplication.isPlaying&&File.Exists("Logs/interface-play.request"))
        {
            File.Delete("Logs/interface-play.request");SessionState.SetBool(Key,true);stage=0;
            EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
        try
        {
            var menu=UnityEngine.Object.FindAnyObjectByType<StartupMenu>();
            var game=UnityEngine.Object.FindAnyObjectByType<MazeBetaGame>();
            if(stage==0){if(!menu){SceneManager.LoadScene("Inicio");next=EditorApplication.timeSinceStartup+5;return;}}
            if(stage==1){Check(menu&&menu.newGameButton.GetComponent<MazeInterfaceButton>(),"Startup design missing");Capture("inicio");}
            if(stage==2)menu.controlsButton.onClick.Invoke();
            if(stage==3){Check(menu.controlsPanel.activeSelf,"Controls button failed");Capture("controles");}
            if(stage==4){menu.closeControlsButton.onClick.Invoke();menu.audioSettings.openButton.onClick.Invoke();}
            if(stage==5){Check(menu.audioSettings.IsOpen,"Audio button failed");Capture("audio");}
            if(stage==6)SceneManager.LoadScene("BetaMaze");
            if(stage>=7&&stage<=14)
            {
                if(!game||!game.Player)return;
                game.recordResults=false;game.bossGracePeriod=10000;game.SkipCinematic();
                var hud=UnityEngine.Object.FindAnyObjectByType<MazeCanvasHUD>();
                if(stage==7){typeof(MazeBetaGame).GetField("paused",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,true);}
                if(stage==8){Check(hud.resultPanel.activeInHierarchy,"Pause missing");Capture("pausa");}
                if(stage==9){hud.primaryButton.onClick.Invoke();Check(!game.IsPaused,"Resume button failed");hud.shopButton.onClick.Invoke();}
                if(stage==10){Check(hud.shopPanel.activeInHierarchy,"Shop missing");Capture("tienda");}
                if(stage==11){game.Progress.AddCoins(20);hud.Refresh();hud.purchaseButtons[0].onClick.Invoke();Check(game.Progress.SpeedSeconds>0,"Purchase button failed");}
                if(stage==12){Capture("tienda-compra");}
                if(stage==13){hud.closeShopButton.onClick.Invoke();Check(!game.ShopOpen,"Shop close failed");}
                if(stage==14){File.WriteAllText("Logs/interface-play-result.txt","PASS: startup, controls, audio, pause/resume, shop/purchase/close. Screenshots captured.");SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;}
            }
            stage++;next=EditorApplication.timeSinceStartup+3;
        }
        catch(Exception error){File.WriteAllText("Logs/interface-play-result.txt",error.ToString());SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;Debug.LogException(error);}
    }
}
