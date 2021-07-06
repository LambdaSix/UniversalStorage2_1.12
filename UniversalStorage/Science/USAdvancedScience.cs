﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;
using KSP.UI.Screens.Flight.Dialogs;
using Experience.Effects;

namespace UniversalStorage2
{
  public class USAdvancedScience : ModuleScienceExperiment, IScienceDataContainer
  {
    [KSPField] public string deployAnimationName = null;
    [KSPField] public string sampleAnimationName = null;
    [KSPField] public string endEventGUIName = "Retract";
    [KSPField] public string startEventGUIName = "Deploy";
    [KSPField] public string toggleEventGUIName = "Toggle";
    [KSPField] public bool deployAvailableInEVA = true;
    [KSPField] public bool deployAvailableInVessel = true;
    [KSPField] public bool deployAvailableInEditor = true;
    [KSPField] public float animSpeed = 1f;

    [KSPField(isPersistant = true)] public bool IsDeployed;

    [KSPField(isPersistant = true)] public bool greeblesActive = true;
    [KSPField] public string greebleTransform = string.Empty;
    [KSPField] public string greebleToggleName = "Toggle Greebles";


    [KSPField] public string deploySampleGUIName = "Deploy Samples";
    [KSPField] public string stowSampleGUIName = "Stow Samples";

    [KSPField] public int experimentsLimit = 1;
    [KSPField(isPersistant = true)] public int experimentsReturned = 0;
    [KSPField(isPersistant = true)] public int experimentsNumber = 0;
    [KSPField] public int concurrentExperimentLimit = -1;
    [KSPField] public bool useSampleTransforms = false;
    [KSPField] public string sampleTransformName = "";

    [KSPField] public string inoperableMessage =
      "<color=orange>[<<1>>]: Cannot run experiment; science module inoperable</color>";

    [KSPField] public string storageFullMessage =
      "<color=orange>[<<1>>]: Cannot run experiment; science module full</color>";

    [KSPField] public string concurrentFullMessage = "Can only store <<1>> sample";

    private Dictionary<int, GameObject> _sampleTransforms = new Dictionary<int, GameObject>();

    private int _dataIndex = 0;
    private List<ScienceData> _initialDataList = new List<ScienceData>();
    private List<ScienceData> _storedScienceReportList = new List<ScienceData>();

    private ExperimentsResultDialog _resultsDialog;
    private PopupDialog _overwriteDialog;

    private Animation _deployAnim;
    private Animation[] _sampleAnims;

    private Transform _greebleTransform;

    private BaseEvent _tglEvent;
    private BaseAction _tglAction;
    private BaseEvent _reviewInitialData;

    private BaseEvent _baseDeployExperiment;
    private BaseEvent _baseDeployExperimentExternal;
    private BaseEvent _baseResetExperiment;
    private BaseEvent _baseResetExperimentExternal;
    private BaseEvent _baseCollectDataExternal;
    private BaseEvent _baseReviewData;
    private BaseEvent _baseCleanExperimentExternal;
    private BaseEvent _baseTrasferData;
    private BaseAction _baseDeployAction;
    private BaseAction _baseResetAction;

    private bool _silentDeploy;

    private string _localizedDeployString = "Deploy";
    private string _localizedRetractString = "Retract";
    private string _localizedSampleDeployString = "Deploy Samples";
    private string _localizedSampleStowString = "Stow Samples";

    private string _localizedToggleActionString = "Toggle";
    private string _localizedGreebleString = "Toggle Greebles";

    private string _localizedInoperabletring =
      "<color=orange>[<<1>>]: Cannot run experiment; science module inoperable</color>";

    private string _localizedStorageFullString =
      "<color=orange>[<<1>>]: Cannot run experiment; science module full</color>";

    private string _localizedConcurrentFullString = "<color=orange>[<<1>>]: Can only store 1 sample at a time";

    private Coroutine _waitForDoors;

    public override void OnAwake()
    {
      base.OnAwake();

      _localizedDeployString = Localizer.Format(startEventGUIName);
      _localizedRetractString = Localizer.Format(endEventGUIName);
      _localizedSampleDeployString = Localizer.Format(deploySampleGUIName);
      _localizedSampleStowString = Localizer.Format(stowSampleGUIName);
      _localizedToggleActionString = Localizer.Format(toggleEventGUIName);
      _localizedGreebleString = Localizer.Format(greebleToggleName);

      _tglEvent = Events["ToggleEvent"];
      _tglAction = Actions["ToggleAction"];

      _baseResetExperiment = Events["ResetExperiment"];
      _baseResetExperimentExternal = Events["ResetExperimentExternal"];
      _baseCollectDataExternal = Events["CollectDataExternalEvent"];
      _baseReviewData = Events["ReviewDataEvent"];
      _reviewInitialData = Events["ReviewInitialData"];
      _baseCleanExperimentExternal = Events["CleanUpExperimentExternal"];
      _baseTrasferData = Events["TransferDataEvent"];

      _baseDeployExperiment = Events["DeployExperiment"];
      _baseDeployExperimentExternal = Events["DeployExperimentExternal"];
      _baseDeployAction = Actions["DeployAction"];
      _baseResetAction = Actions["ResetAction"];
    }

    public override void OnStart(StartState state)
    {
      base.OnStart(state);

      _localizedInoperabletring = Localizer.Format(inoperableMessage, experiment.experimentTitle);
      _localizedStorageFullString = Localizer.Format(storageFullMessage, experiment.experimentTitle);
      _localizedConcurrentFullString = Localizer.Format(concurrentFullMessage, concurrentExperimentLimit);

      if (!string.IsNullOrEmpty(deployAnimationName))
      {
        _deployAnim = part.FindModelAnimator(deployAnimationName);

        if (_deployAnim != null)
          _deployAnim.playAutomatically = false;
      }

      if (!string.IsNullOrEmpty(sampleAnimationName))
      {
        _sampleAnims = new Animation[experimentsLimit];

        for (var i = 0; i < experimentsLimit; i++)
        {
          _sampleAnims[i] = part.FindModelAnimator(sampleAnimationName + (i + 1).ToString());

          if (_sampleAnims[i] != null)
          {
            _sampleAnims[i].playAutomatically = false;
            Animate(-1, 0, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
          }
        }
      }

      if (!string.IsNullOrEmpty(greebleTransform))
      {
        _greebleTransform = part.FindModelTransform(greebleTransform);

        Events["ToggleGreebles"].active = _greebleTransform != null;
        Events["ToggleGreebles"].guiName = _localizedGreebleString;
      }

      _tglEvent.guiActiveUnfocused = deployAvailableInEVA;
      _tglEvent.guiActive = deployAvailableInVessel;
      _tglEvent.guiActiveEditor = deployAvailableInEditor;
      _tglEvent.unfocusedRange = interactionRange;
      _tglAction.guiName = _localizedToggleActionString;

      if (useSampleTransforms)
        SetSampleTransforms();

      if (_sampleAnims != null)
        if (IsDeployed)
          for (var i = 0; i < experimentsNumber; i++)
            if (_sampleAnims.Length > i)
              Animate(1, 1, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);

      if (greeblesActive)
      {
        _tglEvent.active = true;
        _tglAction.active = true;

        if (IsDeployed)
        {
          _tglEvent.guiName = _localizedRetractString;

          _baseDeployExperiment.active = true;
          _baseDeployExperimentExternal.active = true;

          if (_deployAnim != null) Animate(1, 1, WrapMode.Default, deployAnimationName, _deployAnim);
        }
        else
        {
          _tglEvent.guiName = _localizedDeployString;

          _baseDeployExperiment.active = false;
          _baseDeployExperimentExternal.active = false;

          if (_deployAnim != null) Animate(-1, 0, WrapMode.Default, deployAnimationName, _deployAnim);
        }

        if (_greebleTransform != null)
          _greebleTransform.gameObject.SetActive(true);
      }
      else
      {
        if (_greebleTransform != null)
          _greebleTransform.gameObject.SetActive(false);
      }
    }

    public override void OnUpdate()
    {
      base.OnUpdate();

      EventsCheck();
    }

    public override void OnSave(ConfigNode node)
    {
      node.RemoveNodes("ScienceData");
      foreach (var storedData in _storedScienceReportList)
      {
        var storedDataNode = node.AddNode("ScienceData");
        storedData.Save(storedDataNode);
      }
    }

    public override void OnLoad(ConfigNode node)
    {
      if (node.HasNode("ScienceData"))
        foreach (var storedDataNode in node.GetNodes("ScienceData"))
        {
          var data = new ScienceData(storedDataNode);
          _storedScienceReportList.Add(data);
        }
    }

    private void OnPause()
    {
      if (_overwriteDialog != null)
        _overwriteDialog.gameObject.SetActive(false);

      if (_resultsDialog != null)
        _resultsDialog.gameObject.SetActive(false);
    }

    private void OnResume()
    {
      if (_overwriteDialog != null)
        _overwriteDialog.gameObject.SetActive(true);

      if (_resultsDialog != null)
        _resultsDialog.gameObject.SetActive(true);
    }

    private void EventsCheck()
    {
      _baseDeployExperiment.active = (IsDeployed || !greeblesActive) && !Inoperable && _resultsDialog == null;
      _baseDeployExperimentExternal.active = (IsDeployed || !greeblesActive) && !Inoperable && _resultsDialog == null;

      _baseResetExperiment.active = _storedScienceReportList.Count > 0 && _resultsDialog == null;
      _baseResetExperimentExternal.active = _baseResetExperiment.active && resettableOnEVA && _resultsDialog == null;
      _baseResetAction.active = _baseResetExperiment.active;
      _baseCollectDataExternal.active = dataIsCollectable && _storedScienceReportList.Count > 0;
      _baseReviewData.active = _storedScienceReportList.Count > 0 && _resultsDialog == null;
      _reviewInitialData.active = _initialDataList.Count > 0 && _resultsDialog == null;
      _baseCleanExperimentExternal.active = Inoperable && resettableOnEVA && _resultsDialog == null;
      _baseTrasferData.active = hasContainer && dataIsCollectable && _storedScienceReportList.Count > 0;

      if (!greeblesActive)
      {
        _tglEvent.active = _storedScienceReportList.Count > 0 || _initialDataList.Count > 0;
        _tglAction.active = _storedScienceReportList.Count > 0 || _initialDataList.Count > 0;
      }
    }

    private void SetSampleTransforms()
    {
      if (!string.IsNullOrEmpty(sampleTransformName))
      {
        if (experimentsLimit > 1)
        {
          for (var i = 0; i < experimentsLimit; i++)
          {
            var s = sampleTransformName + (i + 1).ToString();

            var t = part.FindModelTransform(s);

            if (t == null || t.gameObject == null)
              continue;

            if (_sampleTransforms.ContainsKey(i))
              continue;

            _sampleTransforms.Add(i, t.gameObject);

            if (experimentsReturned > i)
              t.gameObject.SetActive(false);
          }
        }
        else
        {
          var t = part.FindModelTransform(sampleTransformName);

          if (t != null && t.gameObject != null)
            if (!_sampleTransforms.ContainsKey(0))
            {
              _sampleTransforms.Add(0, t.gameObject);

              if (experimentsReturned > 0)
                t.gameObject.SetActive(false);
            }
        }
      }
    }

    private void Animate(float speed, float time, WrapMode wrap, string name, Animation a)
    {
      if (a != null)
      {
        a[name].speed = speed;

        if (!a.IsPlaying(name))
        {
          a[name].wrapMode = wrap;
          a[name].normalizedTime = time;
          a.Blend(name, 1);
        }
      }
    }

    [KSPEvent(guiActive = true, guiName = "Deploy", active = true)]
    private void ToggleEvent()
    {
      if (IsDeployed)
      {
        if (greeblesActive)
        {
          _tglEvent.guiName = _localizedDeployString;
          _tglEvent.active = false;
          _tglAction.active = false;

          for (var i = experimentsNumber - 1; i >= 0; i--)
          {
            if (_sampleAnims.Length <= i)
              continue;

            if (experimentsReturned > i)
              continue;

            Animate(-1, 1, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
          }

          if (_waitForDoors != null)
            StopCoroutine(_waitForDoors);

          _waitForDoors = StartCoroutine(WaitForSampleStow());
        }
        else
        {
          _tglEvent.guiName = _localizedSampleDeployString;

          for (var i = experimentsNumber - 1; i >= 0; i--)
          {
            if (_sampleAnims.Length <= i)
              continue;

            if (experimentsReturned > i)
              continue;

            Animate(-1, 1, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
          }
        }

        IsDeployed = false;
      }
      else
      {
        if (greeblesActive)
        {
          _tglEvent.guiName = _localizedRetractString;

          if (experimentsNumber > 0 && experimentsReturned < experimentsNumber)
          {
            _tglEvent.active = false;
            _tglAction.active = false;

            Animate(1 * animSpeed, 0, WrapMode.Default, deployAnimationName, _deployAnim);

            if (_waitForDoors != null)
              StopCoroutine(_waitForDoors);

            _waitForDoors = StartCoroutine(WaitForDoorOpen());
          }
          else
          {
            Animate(1 * animSpeed, 0, WrapMode.Default, deployAnimationName, _deployAnim);
          }
        }
        else
        {
          _tglEvent.guiName = _localizedSampleStowString;

          for (var i = experimentsNumber - 1; i >= 0; i--)
          {
            if (_sampleAnims.Length <= i)
              continue;

            if (experimentsReturned > i)
              continue;

            Animate(1, 0, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
          }
        }

        IsDeployed = true;
      }
    }

    private IEnumerator WaitForSampleStow()
    {
      float time = 0;

      for (var i = experimentsNumber - 1; i >= 0; i--)
      {
        if (_sampleAnims.Length <= i)
          continue;

        if (experimentsReturned > i)
          continue;

        var l = _sampleAnims[i][sampleAnimationName + (i + 1).ToString()].length;

        if (l > time)
          time = l;
      }

      yield return new WaitForSeconds(time);

      Animate(-1 * animSpeed, 1, WrapMode.Default, deployAnimationName, _deployAnim);

      yield return new WaitForSeconds(_deployAnim[deployAnimationName].length);

      _tglEvent.active = true;
      _tglAction.active = true;

      _waitForDoors = null;
    }

    private IEnumerator WaitForDoorOpen()
    {
      yield return new WaitForSeconds(_deployAnim[deployAnimationName].length);

      for (var i = experimentsNumber - 1; i >= 0; i--)
      {
        if (_sampleAnims.Length <= i)
          continue;

        if (experimentsReturned > i)
          continue;

        Animate(1, 0, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
      }

      _tglEvent.active = true;
      _tglAction.active = true;

      _waitForDoors = null;
    }

    [KSPAction("Toggle")]
    private void ToggleAction(KSPActionParam param)
    {
      if (deployAvailableInVessel)
        ToggleEvent();
    }

    [KSPEvent(active = true, guiActive = false, guiActiveUnfocused = false, guiActiveUncommand = false,
      guiActiveEditor = true, guiName = "Toggle Greebles")]
    private void ToggleGreebles()
    {
      if (greeblesActive)
      {
        _greebleTransform.gameObject.SetActive(false);

        _tglEvent.active = false;
        _tglAction.active = false;
      }
      else
      {
        _greebleTransform.gameObject.SetActive(true);

        _tglEvent.active = true;
        _tglAction.active = true;
      }

      greeblesActive = !greeblesActive;
    }

    public new void ResetExperiment()
    {
      if (_storedScienceReportList.Count > 0 || _initialDataList.Count > 0)
      {
        if (IsDeployed && _sampleAnims != null)
          for (var i = experimentsNumber - 1; i >= experimentsReturned; i--)
            if (_sampleAnims.Length > i && i >= 0)
              Animate(-1, 1, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);

        if (experimentsLimit > 1)
        {
          foreach (var data in _storedScienceReportList)
            experimentsNumber--;

          foreach (var data in _initialDataList)
            experimentsNumber--;

          _storedScienceReportList.Clear();
          _initialDataList.Clear();

          if (experimentsNumber < 0)
            experimentsNumber = 0;
        }
        else
        {
          experimentsNumber = 0;
          _storedScienceReportList.Clear();
        }
      }

      Deployed = false;
      Inoperable = false;
    }

    public new void ResetAction(KSPActionParam param)
    {
      ResetExperiment();
    }

    public new void ResetExperimentExternal()
    {
      ResetExperiment();
    }

    public new void CollectDataExternalEvent()
    {
      var EVACont = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleScienceContainer>();

      if (EVACont.Count <= 0)
        return;

      if (_storedScienceReportList.Count > 0)
        if (EVACont.First().StoreData(new List<IScienceDataContainer> {this}, false))
          DumpAllData(_storedScienceReportList);
    }

    public new void CleanUpExperimentExternal()
    {
      if (!FlightGlobals.ActiveVessel.isEVA)
        return;

      if (!FlightGlobals.ActiveVessel.parts[0].protoModuleCrew[0].HasEffect<ScienceResetSkill>())
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238776", part.partInfo.title), 5f,
          ScreenMessageStyle.UPPER_LEFT);
        return;
      }

      OnLabReset();

      ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238770", part.partInfo.title), 6f,
        ScreenMessageStyle.UPPER_LEFT);
    }

    private void OnLabReset()
    {
      Inoperable = false;
      Deployed = false;

      experimentsNumber = 0;
      experimentsReturned = 0;

      if (useSampleTransforms)
        for (var i = 0; i < _sampleTransforms.Count; i++)
          if (_sampleTransforms.ContainsKey(i))
            _sampleTransforms[i].SetActive(true);

      if (_sampleAnims != null)
        for (var i = 0; i < _sampleAnims.Length; i++)
          Animate(-1, 0, WrapMode.Default, sampleAnimationName + (i + 1).ToString(), _sampleAnims[i]);
    }

    public new void TransferDataEvent()
    {
      if (PartItemTransfer.Instance != null)
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238816"), 3f, ScreenMessageStyle.UPPER_CENTER);
        return;
      }

      ExperimentTransfer.Create(part, this, new Callback<PartItemTransfer.DismissAction, Part>(transferData));
    }

    private void transferData(PartItemTransfer.DismissAction dismiss, Part p)
    {
      if (dismiss != PartItemTransfer.DismissAction.ItemMoved)
        return;

      if (p == null)
        return;

      if (_storedScienceReportList.Count <= 0)
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238567", part.partInfo.title), 3,
          ScreenMessageStyle.UPPER_CENTER);
        return;
      }

      var container = p.FindModuleImplementing<ModuleScienceContainer>();

      if (container == null)
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238572", part.partInfo.title, p.partInfo.title), 5,
          ScreenMessageStyle.UPPER_CENTER);
        return;
      }

      if (experimentsReturned >= experimentsLimit - 1 && !rerunnable)
      {
        var dialog = new List<DialogGUIBase>();
        dialog.Add(new DialogGUIButton<ModuleScienceContainer>(Localizer.Format("#autoLOC_7003412"),
          new Callback<ModuleScienceContainer>(onTransferData), container));
        dialog.Add(new DialogGUIButton(Localizer.Format("#autoLOC_226976"), null, true));

        PopupDialog.SpawnPopupDialog(
          new Vector2(0.5f, 0.5f),
          new Vector2(0.5f, 0.5f),
          new MultiOptionDialog(
            "DMDataTransfer",
            collectWarningText,
            part.partInfo.title + Localizer.Format("#autoLOC_236416"),
            UISkinManager.defaultSkin,
            dialog.ToArray()
          ),
          false,
          UISkinManager.defaultSkin,
          true,
          ""
        );
      }
      else
      {
        onTransferData(container);
      }
    }

    private void onTransferData(ModuleScienceContainer target)
    {
      if (target == null)
        return;

      if (target.StoreData(new List<IScienceDataContainer> {this}, false))
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238582", target.part.partInfo.title), 5,
          ScreenMessageStyle.UPPER_LEFT);
      else
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238589", target.part.partInfo.title), 5,
          ScreenMessageStyle.UPPER_LEFT);
    }

    public new void DeployExperimentExternal()
    {
      DeployExperiment();
    }

    public new void DeployAction(KSPActionParam param)
    {
      DeployExperiment();
    }

    public new void DeployExperiment()
    {
      GatherScienceData();
    }

    public void GatherScienceData(bool silent = false)
    {
      if (CanConduct(silent))
      {
        if (greeblesActive && !IsDeployed && _deployAnim != null)
        {
          ToggleEvent();

          _tglEvent.active = false;
          _tglAction.active = false;

          StartCoroutine(WaitForDeploy(silent, false));
        }
        else
        {
          RunExperiment(silent, false);
        }
      }
    }

    private void OverrideGatherScienceData()
    {
      GatherScience(_silentDeploy);
    }

    private void GatherScience(bool silent)
    {
      if (greeblesActive && !IsDeployed && _deployAnim != null)
      {
        ToggleEvent();

        _tglEvent.active = false;
        _tglAction.active = false;

        StartCoroutine(WaitForDeploy(silent, true));
      }
      else
      {
        RunExperiment(silent, true);
      }
    }

    private bool CanConduct(bool silent)
    {
      if (Inoperable)
      {
        ScreenMessages.PostScreenMessage(_localizedInoperabletring, 5, ScreenMessageStyle.UPPER_LEFT);
        return false;
      }

      if (!experiment.IsAvailableWhile(ScienceUtil.GetExperimentSituation(vessel), vessel.mainBody))
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238424", experiment.experimentTitle), 5,
          ScreenMessageStyle.UPPER_CENTER);
        return false;
      }

      if (FlightGlobals.ActiveVessel.isEVA)
        if (!ScienceUtil.RequiredUsageExternalAvailable(part.vessel, FlightGlobals.ActiveVessel,
          (ExperimentUsageReqs) usageReqMaskExternal
          , experiment, ref usageReqMessage))
        {
          ScreenMessages.PostScreenMessage(usageReqMessage, 5, ScreenMessageStyle.UPPER_CENTER);
          return false;
        }

      if (concurrentExperimentLimit > 0 &&
          _storedScienceReportList.Count + _initialDataList.Count >= concurrentExperimentLimit)
      {
        _silentDeploy = silent;
        _overwriteDialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
          , new MultiOptionDialog("OverwriteExperiment", _localizedConcurrentFullString
            , Localizer.Format("#autoLOC_238371", experiment.experimentTitle)
            , HighLogic.UISkin, new DialogGUIBase[]
            {
              new DialogGUIButton(experimentActionName, new Callback(OverrideGatherScienceData)),
              new DialogGUIButton(Localizer.Format("#autoLOC_253501"), null)
            }), false, HighLogic.UISkin, true, string.Empty);
        return false;
      }

      if (Deployed)
      {
        ScreenMessages.PostScreenMessage(_localizedStorageFullString, 5, ScreenMessageStyle.UPPER_LEFT);

        //_silentDeploy = silent;
        //_overwriteDialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)
        //    , new MultiOptionDialog("OverwriteExperiment", Localizer.Format("#autoLOC_238370", experiment.experimentTitle)
        //    , Localizer.Format("#autoLOC_238371", experiment.experimentTitle)
        //    , HighLogic.UISkin, new DialogGUIBase[]
        //    {
        //        new DialogGUIButton(experimentActionName, new Callback(OverrideGatherScienceData)),
        //        new DialogGUIButton(Localizer.Format("#autoLOC_253501"), null)
        //    }), false, HighLogic.UISkin, true, string.Empty);

        return false;
      }

      _silentDeploy = false;

      return true;
    }

    private IEnumerator WaitForDeploy(bool silent, bool overwrite)
    {
      yield return new WaitForSeconds(_deployAnim[deployAnimationName].length);

      _tglEvent.active = true;
      _tglAction.active = true;

      RunExperiment(silent, overwrite);
    }

    private void RunExperiment(bool silent, bool overwrite)
    {
      var data = MakeData();

      if (data == null)
        return;

      GameEvents.OnExperimentDeployed.Fire(data);

      if (!overwrite)
        experimentsNumber++;

      IsDeployed = true;

      if (!overwrite && _sampleAnims != null && _sampleAnims.Length >= experimentsNumber)
        Animate(1, 0, WrapMode.Default, sampleAnimationName + experimentsNumber.ToString(),
          _sampleAnims[experimentsNumber - 1]);

      if (experimentsLimit <= 1)
      {
        _dataIndex = 0;

        if (overwrite && _storedScienceReportList.Count > 0)
          _storedScienceReportList[0] = data;
        else
          _storedScienceReportList.Add(data);

        Deployed = true;

        if (!silent)
          ReviewData();
        else
          ScreenMessages.PostScreenMessage(
            Localizer.Format("#autoLOC_238419", part.partInfo.title, data.dataAmount.ToString(), data.title), 8,
            ScreenMessageStyle.UPPER_LEFT);
      }
      else
      {
        if (overwrite)
        {
          if (_initialDataList.Count > 0)
          {
            _initialDataList[0] = data;
          }
          else
          {
            if (_storedScienceReportList.Count > 0)
            {
              _storedScienceReportList.RemoveAt(_storedScienceReportList.Count - 1);

              _initialDataList.Add(data);
            }
            else
            {
              _initialDataList.Add(data);
            }
          }
        }
        else
        {
          _initialDataList.Add(data);
        }

        Deployed = experimentsNumber >= experimentsLimit;

        if (silent)
        {
          onKeepInitialData(data);

          ScreenMessages.PostScreenMessage(
            Localizer.Format("#autoLOC_238419", part.partInfo.title, data.dataAmount.ToString(), data.title), 8,
            ScreenMessageStyle.UPPER_LEFT);
        }
        else
        {
          initialResultsPage();
        }
      }

      if (!greeblesActive)
        _tglEvent.guiName = _localizedSampleStowString;
    }

    private ScienceData MakeData()
    {
      var situation = ScienceUtil.GetExperimentSituation(vessel);

      var biome = "";
      var biomeDisplay = "";

      if (experiment.BiomeIsRelevantWhile(situation))
      {
        if (string.IsNullOrEmpty(vessel.landedAt))
        {
          biome = ScienceUtil.GetExperimentBiome(vessel.mainBody, vessel.latitude, vessel.longitude);
          biomeDisplay = ScienceUtil.GetBiomedisplayName(vessel.mainBody, biome);
        }
        else
        {
          biome = Vessel.GetLandedAtString(vessel.landedAt);
          biomeDisplay = Localizer.Format(vessel.displaylandedAt);
        }

        if (string.IsNullOrEmpty(biomeDisplay))
          biomeDisplay = biome;
      }

      var subject =
        ResearchAndDevelopment.GetExperimentSubject(experiment, situation, vessel.mainBody, biome, biomeDisplay);

      var data = new ScienceData(experiment.baseValue * experiment.dataScale, xmitDataScalar, 0, subject.id,
        subject.title, false, part.flightID);

      return data;
    }

    ScienceData[] IScienceDataContainer.GetData()
    {
      return GetData();
    }

    int IScienceDataContainer.GetScienceCount()
    {
      return GetScienceCount();
    }

    bool IScienceDataContainer.IsRerunnable()
    {
      return IsRerunnable();
    }

    void IScienceDataContainer.ReturnData(ScienceData data)
    {
      ReturnData(data);
    }

    void IScienceDataContainer.ReviewData()
    {
      ReviewData();
    }

    void IScienceDataContainer.ReviewDataItem(ScienceData data)
    {
      ReviewData();
    }

    void IScienceDataContainer.DumpData(ScienceData data)
    {
      DumpData(data);
    }

    public new ScienceData[] GetData()
    {
      var DataList = new List<ScienceData>();

      DataList.AddRange(_storedScienceReportList);
      DataList.AddRange(_initialDataList);

      return DataList.ToArray();
    }

    public new int GetScienceCount()
    {
      return _storedScienceReportList.Count + _initialDataList.Count;
    }

    private new void ReturnData(ScienceData data)
    {
      if (data == null)
        return;

      _storedScienceReportList.Add(data);

      experimentsReturned--;

      if (experimentsReturned < 0)
        experimentsReturned = 0;

      Inoperable = false;

      if (experimentsLimit <= 1)
        Deployed = true;
      else
        Deployed = experimentsNumber >= experimentsLimit;

      if (useSampleTransforms)
      {
        var i = experimentsReturned;

        if (_sampleTransforms.ContainsKey(i))
        {
          var g = _sampleTransforms[i];

          if (g != null)
            g.SetActive(true);
        }
      }
    }

    private new bool IsRerunnable()
    {
      if (rerunnable)
        return true;
      else
        return experimentsReturned < experimentsLimit;
    }

    private new void DumpData(ScienceData data)
    {
      if (_storedScienceReportList.Contains(data))
      {
        experimentsReturned++;
        Inoperable = !IsRerunnable();
        Deployed = Inoperable;
        _storedScienceReportList.Remove(data);

        if (useSampleTransforms)
        {
          var i = experimentsReturned - 1;

          if (_sampleTransforms.ContainsKey(i))
          {
            var g = _sampleTransforms[i];

            if (g != null)
              g.SetActive(false);
          }
        }
      }
      else if (_initialDataList.Contains(data))
      {
        DumpInitialData(data);
      }
    }

    private void DumpInitialData(ScienceData data)
    {
      if (_initialDataList.Count > 0)
      {
        experimentsReturned++;
        Inoperable = !IsRerunnable();
        Deployed = Inoperable;
        _initialDataList.Remove(data);

        if (useSampleTransforms)
        {
          var i = experimentsReturned - 1;

          if (_sampleTransforms.ContainsKey(i))
          {
            var g = _sampleTransforms[i];

            if (g != null)
              g.SetActive(false);
          }
        }
      }
    }

    private void DumpAllData(List<ScienceData> data)
    {
      foreach (var d in data)
        experimentsReturned++;

      Inoperable = !IsRerunnable();
      Deployed = Inoperable;
      data.Clear();

      if (useSampleTransforms)
        for (var i = 1; i <= data.Count; i++)
        {
          var j = experimentsReturned - i;

          if (_sampleTransforms.ContainsKey(j))
          {
            var g = _sampleTransforms[j];

            if (g != null)
              g.SetActive(false);
          }
        }
    }

    private void newResultPage()
    {
      if (_storedScienceReportList.Count > 0)
      {
        var data = _storedScienceReportList[_dataIndex];
        var page = new ExperimentResultDialogPage(part, data, data.baseTransmitValue, data.transmitBonus,
          experimentsReturned >= experimentsLimit - 1 && !rerunnable, transmitWarningText, true,
          new ScienceLabSearch(vessel, data), new Callback<ScienceData>(onDiscardData),
          new Callback<ScienceData>(onKeepData), new Callback<ScienceData>(onTransmitData),
          new Callback<ScienceData>(onSendToLab));
        _resultsDialog = ExperimentsResultDialog.DisplayResult(page);
      }
    }

    public new void ReviewData()
    {
      _dataIndex = 0;
      foreach (var data in _storedScienceReportList)
      {
        newResultPage();
        _dataIndex++;
      }
    }

    public new void ReviewDataEvent()
    {
      ReviewData();
    }

    [KSPEvent(guiActive = true, guiName = "Review Initial Data", active = false)]
    public void ReviewInitialData()
    {
      if (_initialDataList.Count > 0)
        initialResultsPage();
    }

    private void initialResultsPage()
    {
      if (_initialDataList.Count > 0)
      {
        var data = _initialDataList[0];
        var page = new ExperimentResultDialogPage(part, data, data.baseTransmitValue, data.transmitBonus,
          experimentsReturned >= experimentsLimit - 1 && !rerunnable, transmitWarningText, true,
          new ScienceLabSearch(vessel, data), new Callback<ScienceData>(onDiscardInitialData),
          new Callback<ScienceData>(onKeepInitialData), new Callback<ScienceData>(onTransmitInitialData),
          new Callback<ScienceData>(onSendInitialToLab));
        _resultsDialog = ExperimentsResultDialog.DisplayResult(page);
      }
    }

    private void onDiscardData(ScienceData data)
    {
      _resultsDialog = null;

      if (_storedScienceReportList.Count > 0)
      {
        _storedScienceReportList.Remove(data);

        if (IsDeployed && _sampleAnims != null && _sampleAnims.Length >= experimentsNumber)
          Animate(-1, 1, WrapMode.Default, sampleAnimationName + experimentsNumber.ToString(),
            _sampleAnims[experimentsNumber - 1]);

        experimentsNumber--;

        if (experimentsNumber < 0)
          experimentsNumber = 0;

        Deployed = false;
      }
    }

    private void onKeepData(ScienceData data)
    {
      _resultsDialog = null;
    }

    private void onTransmitData(ScienceData data)
    {
      _resultsDialog = null;

      var bestTransmitter = ScienceUtil.GetBestTransmitter(vessel);

      if (bestTransmitter != null)
      {
        bestTransmitter.TransmitData(new List<ScienceData> {data});
        DumpData(data);
      }
      else if (CommNet.CommNetScenario.CommNetEnabled)
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238505"), 3f, ScreenMessageStyle.UPPER_CENTER);
      }
      else
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238507"), 3f, ScreenMessageStyle.UPPER_CENTER);
      }
    }

    private void onSendToLab(ScienceData data)
    {
      _resultsDialog = null;

      var labSearch = new ScienceLabSearch(vessel, data);

      if (labSearch.NextLabForDataFound)
      {
        StartCoroutine(labSearch.NextLabForData.ProcessData(data, null));
        DumpData(data);
      }
      else
      {
        labSearch.PostErrorToScreen();
      }
    }

    private void onDiscardInitialData(ScienceData data)
    {
      _resultsDialog = null;

      if (_initialDataList.Count > 0)
      {
        _initialDataList.Remove(data);

        if (IsDeployed && _sampleAnims != null && _sampleAnims.Length >= experimentsNumber)
          Animate(-1, 1, WrapMode.Default, sampleAnimationName + experimentsNumber.ToString(),
            _sampleAnims[experimentsNumber - 1]);

        experimentsNumber--;

        if (experimentsNumber < 0)
          experimentsNumber = 0;

        Deployed = false;
      }
    }

    private void onKeepInitialData(ScienceData data)
    {
      _resultsDialog = null;

      if (experimentsNumber > experimentsLimit)
      {
        ScreenMessages.PostScreenMessage(_localizedStorageFullString, 5f, ScreenMessageStyle.UPPER_CENTER);
        initialResultsPage();
      }
      else if (_initialDataList.Count > 0)
      {
        _storedScienceReportList.Add(data);
        _initialDataList.Remove(data);
      }
    }

    private void onTransmitInitialData(ScienceData data)
    {
      _resultsDialog = null;

      var bestTransmitter = ScienceUtil.GetBestTransmitter(vessel);

      if (bestTransmitter != null)
      {
        bestTransmitter.TransmitData(new List<ScienceData> {data});
        DumpInitialData(data);
      }
      else if (CommNet.CommNetScenario.CommNetEnabled)
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238505"), 3f, ScreenMessageStyle.UPPER_CENTER);
      }
      else
      {
        ScreenMessages.PostScreenMessage(Localizer.Format("#autoLOC_238507"), 3f, ScreenMessageStyle.UPPER_CENTER);
      }
    }

    private void onSendInitialToLab(ScienceData data)
    {
      _resultsDialog = null;

      var labSearch = new ScienceLabSearch(vessel, data);

      if (labSearch.NextLabForDataFound)
      {
        StartCoroutine(labSearch.NextLabForData.ProcessData(data, null));
        DumpInitialData(data);
      }
      else
      {
        labSearch.PostErrorToScreen();
      }
    }
  }
}