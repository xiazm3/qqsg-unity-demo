using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WildBoar.GUIModule;

public class MainPanel : PanelBase
{
    public SceneUnitHUD EnemyHUD;
    public void Start()
    {
        
        DontDestroyOnLoad(this.gameObject);
        EnemyHUD.Hide();

        ConfigureUICameraOverlay();
    }

    public void OnEnable()
    {
        MouseManager.OnClickSceneUnitMaybeNull += OnClickSceneObj;
        ConfigureUICameraOverlay();
    }

    public void OnDisable()
    {
        MouseManager.OnClickSceneUnitMaybeNull -= OnClickSceneObj;
    }

    public void OnClickSceneObj(IHasSceneUnitInfo sceneUnitInfoOwner)
    {
        if (sceneUnitInfoOwner == null)
        {
            EnemyHUD.Hide();
            return;
        }

        EnemyHUD.Show();
        EnemyHUD.SetInfo(sceneUnitInfoOwner.GetSceneUnit());
    }

    private void ConfigureUICameraOverlay()
    {
        var canvas = GetComponent<NUICanvas>();
        if (canvas == null) return;
        var uiCam = canvas.Camera;
        if (uiCam == null) return;

        var uiData = uiCam.GetComponent<UniversalAdditionalCameraData>();
        if (uiData != null) uiData.renderType = CameraRenderType.Overlay;
        uiCam.clearFlags = CameraClearFlags.Depth;

        var baseCam = Camera.main;
        if (baseCam == null)
        {
            var cams = GameObject.FindObjectsOfType<Camera>();
            foreach (var c in cams)
            {
                if (c != uiCam && c.enabled && c.targetTexture == null)
                {
                    baseCam = c;
                    break;
                }
            }
        }
        if (baseCam == null) return;

        var baseData = baseCam.GetComponent<UniversalAdditionalCameraData>();
        if (baseData == null) return;
        if (!baseData.cameraStack.Contains(uiCam)) baseData.cameraStack.Add(uiCam);
        baseData.renderType = CameraRenderType.Base;
    }


}
