﻿using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KSP.UI.Screens;

namespace UniversalStorage2.StockVariants
{
  public class EditorPartIconListener : MonoBehaviour
  {
    private EditorPartIcon icon;

    private bool _activeSwitcher;

    private Transform _partIconTransform;
    private AvailablePart _partInfo;

    private EventData<AvailablePart> onPrimaryVariantSwitched = GameEvents.FindEvent
      <EventData<AvailablePart>>("onUSEditorPrimaryVariantSwitched");

    private EventData<AvailablePart> onSecondaryVariantSwitched = GameEvents.FindEvent
      <EventData<AvailablePart>>("onUSEditorSecondaryVariantSwitched");

    private void Start()
    {
      if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedScene == GameScenes.SPACECENTER)
      {
        icon = GetComponentInParent<EditorPartIcon>();

        PartIconSpawn();

        //USVariantController.OnEditorPartIconSpawn.Invoke(icon);
      }
    }

    private void OnDestroy()
    {
      if (onSecondaryVariantSwitched != null)
        onSecondaryVariantSwitched.Remove(OnSecondaryVariantSwitch);

      if (onPrimaryVariantSwitched != null)
        onPrimaryVariantSwitched.Remove(OnPrimaryVariantSwitch);
    }

    private void PartIconSpawn()
    {
      //USdebugMessages.USStaticLog("Parsing Editor Icon...");

      if (icon == null)
        return;

      if (!icon.isPart)
        return;

      if (icon.partInfo == null)
        return;

      if (icon.partInfo.partPrefab == null)
        return;

      if (icon.partInfo.moduleInfo == null)
        return;

      var flag = false;

      for (var i = icon.partInfo.moduleInfos.Count - 1; i >= 0; i--)
        if (icon.partInfo.moduleInfos[i].moduleName == "USSwitch Control")
        {
          //USdebugMessages.USStaticLog("USSwitchControl Module Found");

          flag = true;
          break;
        }

      if (!flag)
        return;

      var switches = icon.partInfo.partPrefab.Modules.GetModules<USSwitchControl>();

      //USdebugMessages.USStaticLog("Parsing Switch Control Modules: {0}", switches.Count);

      flag = false;

      var secondary = false;

      for (var i = 0; i < switches.Count; i++)
        if (switches[i].HasSwitches())
        {
          if (i == 0)
          {
            flag = true;

            //USdebugMessages.USStaticLog("Primary Switch Control Found");

            USVariantController.Instance.AddSwitchControl(icon.partInfo, switches[i], true);
          }
          else if (i == 1)
          {
            secondary = true;

            //USdebugMessages.USStaticLog("Secondary Switch Control Found");

            USVariantController.Instance.AddSwitchControl(icon.partInfo, switches[i], false);
          }
        }

      if (!flag)
        return;

      _partInfo = icon.partInfo;

      //USdebugMessages.USStaticLog("Editor icon: {0}", icon.partInfo.iconPrefab.name);

      var clone = _partInfo.iconPrefab.name + "(Clone)";

      var children = icon.GetComponentsInChildren<Transform>(true);

      for (var i = children.Length - 1; i >= 0; i--)
        if (children[i].name == clone)
        {
          //USdebugMessages.USStaticLog("Found Editor icon: {0}", children[i].name);

          _partIconTransform = children[i];

          break;
        }

      if (_partIconTransform == null)
        return;

      if (HighLogic.LoadedSceneIsEditor)
      {
        //USdebugMessages.USStaticLog("Activating US Switch icon buttons");

        icon.btnSwapTexture.gameObject.SetActive(true);

        icon.btnSwapTexture.onClick.RemoveAllListeners();

        if (secondary)
        {
          var secondaryButton = Instantiate(icon.btnSwapTexture, icon.btnSwapTexture.transform.parent, false);

          var secondRect = secondaryButton.GetComponent<RectTransform>();

          secondRect.anchorMin = new Vector2(1, 0);
          secondRect.anchorMax = new Vector2(1, 0);

          secondRect.anchoredPosition = new Vector2(-4, secondRect.anchoredPosition.y);

          secondaryButton.onClick.AddListener(delegate { ToggleSecondaryVariant(_partInfo); });
        }

        icon.btnSwapTexture.onClick.AddListener(delegate { TogglePrimaryVariant(_partInfo); });
      }

      if (onPrimaryVariantSwitched != null)
        onPrimaryVariantSwitched.Add(OnPrimaryVariantSwitch);

      if (switches != null && switches.Count > 0)
        switches[0].EditorToggleVariant(_partInfo, _partIconTransform, false);

      if (secondary)
      {
        if (onSecondaryVariantSwitched != null)
          onSecondaryVariantSwitched.Add(OnSecondaryVariantSwitch);

        if (switches != null && switches.Count > 1)
          switches[1].EditorToggleVariant(_partInfo, _partIconTransform, false);
      }

      //USdebugMessages.USStaticLog("Original Icon: {7}: Scale: {0:F4} - Rotation: {1:F4} - Position: {2:F4}\nChild: {6}: Scale: {3:F4} - Rotation: {4:F4} - Position: {5:F4}"
      //    , _partIconTransform.localScale, _partIconTransform.localRotation, _partIconTransform.localPosition
      //    , _partIconTransform.GetChild(0).localScale, _partIconTransform.GetChild(0).localRotation, _partIconTransform.GetChild(0).localPosition
      //    , _partIconTransform.GetChild(0).name, _partIconTransform.name);

      UpdateIconScale();
    }

    private void UpdateIconScale()
    {
      if (_partIconTransform == null)
        return;

      if (_partIconTransform.childCount < 1)
        return;

      var scaler = _partIconTransform.GetChild(0);

      var bounder = USTools.GetActivePartBounds(scaler.gameObject);

      var scale = USTools.GetBoundsScale(bounder);

      //USdebugMessages.USStaticLog("New bounds scale: {0}\nIcon Scale: {1}\nBounds Center: {2}\nBounds X: {3} Y: {4} Z: {5}"
      //    , scale.ToString("F4"), _partInfo.iconScale.ToString("F4"), bounder.center.ToString("F3")
      //    , bounder.size.x.ToString("F3"), bounder.size.y.ToString("F3"), bounder.size.y.ToString("F3"));

      scaler.localScale = Vector3.one * scale;
      scaler.localPosition =
        new Vector3(scaler.localPosition.x, (bounder.center * scale * -1f).y, scaler.localPosition.z);

      //USdebugMessages.USStaticLog("New Icon: {7}: Scale: {0:F4} - Rotation: {1:F4} - Position: {2:F4}\nChild: {6}: Scale: {3:F4} - Rotation: {4:F4} - Position: {5:F4}"
      //    , _partIconTransform.localScale, _partIconTransform.localRotation, _partIconTransform.localPosition
      //    , scaler.localScale, scaler.localRotation, scaler.localPosition, scaler.name, _partIconTransform.name);
    }

    private void TogglePrimaryVariant(AvailablePart partInfo)
    {
      var switchControl = USVariantController.Instance.GetSwitchControl(partInfo, true);

      if (switchControl == null)
        return;

      if (_partInfo == null || partInfo != _partInfo)
        return;

      if (_partIconTransform == null)
        return;

      _activeSwitcher = true;

      //USdebugMessages.USStaticLog("Toggle primary variant event for icon: {0}", partInfo.title);

      switchControl.EditorToggleVariant(partInfo, _partIconTransform, true);

      if (onPrimaryVariantSwitched != null)
        onPrimaryVariantSwitched.Fire(partInfo);

      _activeSwitcher = false;

      UpdateIconScale();

      //USdebugMessages.USStaticLog("Select variant from Editor Icon: {0} - Name: {1}", partInfo.title, partIcon.name);
    }

    private void ToggleSecondaryVariant(AvailablePart partInfo)
    {
      var switchControl = USVariantController.Instance.GetSwitchControl(partInfo, false);

      if (switchControl == null)
        return;

      if (_partInfo == null || partInfo != _partInfo)
        return;

      if (_partIconTransform == null)
        return;

      _activeSwitcher = true;

      //USdebugMessages.USStaticLog("Toggle secondary variant event for icon: {0}", partInfo.title);

      switchControl.EditorToggleVariant(partInfo, _partIconTransform, true);

      if (onSecondaryVariantSwitched != null)
        onSecondaryVariantSwitched.Fire(partInfo);

      _activeSwitcher = false;

      UpdateIconScale();

      //USdebugMessages.USStaticLog("Select variant from secondary Editor Icon: {0} - Name: {1}", partInfo.title, partIcon.name);
    }

    private void OnPrimaryVariantSwitch(AvailablePart partInfo)
    {
      if (_activeSwitcher)
        return;

      if (_partInfo == null || partInfo != _partInfo)
        return;

      if (_partIconTransform == null)
        return;

      var switchControl = USVariantController.Instance.GetSwitchControl(partInfo, true);

      if (switchControl == null)
        return;

      //USdebugMessages.USStaticLog("Fire primary variant event for icon: {0}", partInfo.title);

      switchControl.EditorToggleVariant(partInfo, _partIconTransform, false);

      UpdateIconScale();
    }

    private void OnSecondaryVariantSwitch(AvailablePart partInfo)
    {
      if (_activeSwitcher)
        return;

      if (_partInfo == null || partInfo != _partInfo)
        return;

      if (_partIconTransform == null)
        return;

      var switchControl = USVariantController.Instance.GetSwitchControl(partInfo, false);

      if (switchControl == null)
        return;

      //USdebugMessages.USStaticLog("Fire secondary variant event for icon: {0}", partInfo.title);

      switchControl.EditorToggleVariant(partInfo, _partIconTransform, false);

      UpdateIconScale();
    }
  }
}