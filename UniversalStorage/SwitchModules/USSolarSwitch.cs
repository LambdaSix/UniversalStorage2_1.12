﻿using System;
using UnityEngine;
using KSP.Localization;

namespace UniversalStorage2
{
  public class USSolarSwitch : USBaseSwitch, IPartCostModifier
  {
    [KSPField] public string AddedCost = string.Empty;
    [KSPField] public bool DisplayCurrentModeCost = false;

    [KSPField(guiActive = false, guiActiveEditor = false, guiName = "#autoLOC_US_SolarPanelCostField")]
    public float AddedCostValue = 0f;

    [KSPField] public string chargeRate = string.Empty;
    [KSPField] public string secondaryTransformName = string.Empty;
    [KSPField] public string solarMeshTransformName = string.Empty;
    [KSPField] public string resourceName;

    [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#autoLOC_234370")]
    public string SolarPanels;

    [KSPField(guiUnits = " ", guiFormat = "P0", guiActive = true, guiActiveEditor = false,
      guiName = "#autoLOC_6001420")]
    public float SunExposure;

    [KSPField(guiUnits = " ", guiFormat = "F3", guiActive = true, guiActiveEditor = false,
      guiName = "#autoLOC_6001421")]
    public float EnergyFlow;

    [KSPField(isPersistant = true)] public int CurrentSelection = 0;
    [KSPField(isPersistant = true)] public bool IsActive = false;
    [KSPField(isPersistant = true)] public bool IsDeployed = false;
    [KSPField] public bool DebugMode = false;
    [KSPField] public bool SolarPanelsLocked = false;

    private Shader _DebugLineShader;

    private Transform[] _sunCatchers;
    private Transform[] _solarMeshes;

    private float[] _Costs;

    private BaseEvent _tglEvent;

    private BaseField _solarPanelCostField;
    private BaseField _sunExposureField;
    private BaseField _energyFlowField;
    private BaseField _solarPanelField;

    private bool _panelsAvailable;

    private USdebugMessages debug;
    private float[] _chargeRates;
    private float _currentChargeRate;
    private float _panelChargeRate;

    private float _energyFlow;
    private float _sunExposure;

    private int _blockedParts;
    private int _blockedBodies;

    private string _obscuringBody = "";
    private string _obscuringPart = "";

    private string _localizedSunlightString = "Direct Sunlight";
    private string _localizedUnderwaterString = ", Underwater";
    private string _localizedActiveString = "Active";
    private string _localizedInactiveString = "Inactive";
    private string _localizedRetractedString = "Retracted";
    private string _localizedUnavailableString = "Unavailable";

    private bool _started;

    public void DeploySolarPanels(bool isOn)
    {
      if (SolarPanelsLocked)
        return;

      if (!IsActive)
        return;

      IsDeployed = isOn;

      if (HighLogic.LoadedSceneIsFlight)
        SolarPanels = IsDeployed ? "" : _localizedRetractedString;
    }

    public override void OnStart(StartState state)
    {
      base.OnStart(state);

      _localizedSunlightString = Localizer.Format("#autoLOC_235418");
      _localizedUnderwaterString = Localizer.Format("#autoLOC_235468");
      _localizedActiveString = Localizer.Format("#autoLOC_900336");
      _localizedInactiveString = Localizer.Format("#autoLOC_257023");
      _localizedRetractedString = Localizer.Format("#autoLOC_234861");
      _localizedUnavailableString = Localizer.Format("#autoLOC_260121");

      _solarPanelCostField = Fields["AddedCostValue"];
      _solarPanelCostField.guiActiveEditor = DisplayCurrentModeCost;

      _sunExposureField = Fields["SunExposure"];
      _sunExposureField.guiActive = IsActive;

      _energyFlowField = Fields["EnergyFlow"];
      _energyFlowField.guiActive = IsActive;

      _solarPanelField = Fields["SolarPanels"];
      _solarPanelField.guiActive = IsActive;

      _tglEvent = Events["toggleSolarPanels"];

      if (SolarPanelsLocked)
      {
        _sunExposureField.guiActive = false;
        _energyFlowField.guiActive = false;
        _solarPanelField.guiActive = false;
        _solarPanelField.guiActiveEditor = false;
        _solarPanelCostField.guiActiveEditor = false;
        _tglEvent.active = false;

        return;
      }

      debug = new USdebugMessages(DebugMode, "USSolarSwitch");

      _DebugLineShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

      if (string.IsNullOrEmpty(chargeRate))
        return;

      _chargeRates = USTools.parseSingles(chargeRate).ToArray();

      if (HighLogic.LoadedSceneIsEditor)
        SolarPanels = IsActive ? _localizedActiveString : _localizedInactiveString;
      else
        SolarPanels = IsDeployed ? "" : _localizedRetractedString;
    }

    public override void OnStartFinished(StartState state)
    {
      base.OnStartFinished(state);

      if (SolarPanelsLocked)
        return;

      if (!string.IsNullOrEmpty(AddedCost))
        _Costs = USTools.parseSingles(AddedCost).ToArray();

      if (string.IsNullOrEmpty(secondaryTransformName))
        return;

      _sunCatchers = part.FindModelTransforms(secondaryTransformName);

      if (DebugMode)
        debug.debugMessage(string.Format("Sun Catchers Found: {0}", _sunCatchers.Length));

      if (string.IsNullOrEmpty(solarMeshTransformName))
        return;

      _solarMeshes = part.FindModelTransforms(solarMeshTransformName);

      for (var i = _solarMeshes.Length - 1; i >= 0; i--) _solarMeshes[i].gameObject.SetActive(IsActive);

      if (DebugMode)
        debug.debugMessage(string.Format("Solar Meshes Found: {0}", _solarMeshes.Length));

      UpdateSolarPanels();

      _started = true;
    }

    [KSPEvent(name = "toggleSolarPanels", guiName = "#autoLOC_US_ToggleSolarPanel", guiActive = false,
      guiActiveEditor = true, guiActiveUnfocused = false, active = true)]
    private void toggleSolarPanels()
    {
      if (SolarPanelsLocked)
        return;

      if (!_panelsAvailable)
        return;

      if (!HighLogic.LoadedSceneIsEditor)
        return;

      IsActive = !IsActive;

      if (_solarMeshes != null)
        for (var i = _solarMeshes.Length - 1; i >= 0; i--)
          _solarMeshes[i].gameObject.SetActive(IsActive);

      if (DebugMode)
        debug.debugMessage(string.Format("Toggle solar panels active: {0}", IsActive));

      _sunExposureField.guiActive = IsActive;
      _energyFlowField.guiActive = IsActive;

      SolarPanels = IsActive ? _localizedActiveString : _localizedInactiveString;

      GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }

    private void Update()
    {
      if (SolarPanelsLocked)
        return;

      if (!_panelsAvailable)
        return;

      if (!_started)
        return;

      if (!DebugMode)
        return;

      DrawDebugLines(_sunCatchers, 1, Color.yellow);
    }

    //Borrowed mostly from Nertea's Near Future Solar - ModuleCurvedSolarPanel
    //https://github.com/ChrisAdderley/NearFutureSolar/blob/master/Source/ModuleCurvedSolarPanel.cs
    private void FixedUpdate()
    {
      if (SolarPanelsLocked)
        return;

      if (!_panelsAvailable)
        return;

      if (!_started)
        return;

      if (!IsActive)
        return;

      if (!IsDeployed)
        return;

      if (!HighLogic.LoadedSceneIsFlight)
        return;

      _sunExposure = 0;
      _energyFlow = 0;

      _blockedParts = 0;
      _blockedBodies = 0;

      for (var i = 0; i < _sunCatchers.Length; i++)
      {
        var sunCatcher = _sunCatchers[i];

        if (SolarLOS(sunCatcher, out var angle, out _obscuringBody))
        {
          if (PartLOS(sunCatcher, out _obscuringPart))
          {
            _sunExposure += Mathf.Clamp01(Mathf.Cos(angle * Mathf.Deg2Rad)) / _sunCatchers.Length;
            _energyFlow = _sunExposure;
          }
          else
          {
            _sunExposure += 0f;
            _energyFlow += 0f;
            _blockedParts++;
          }
        }
        else
        {
          _sunExposure += 0f;
          _energyFlow += 0f;
          _blockedBodies++;
        }
      }

      var distFromSun = vessel.solarFlux / PhysicsGlobals.SolarLuminosityAtHome;

      var realFlow = _energyFlow * distFromSun;

      SunExposure = _sunExposure;

      SolarPanels = _localizedSunlightString;

      if (part.submergedPortion > 0 && _sunCatchers != null && _sunCatchers.Length > 0)
      {
        double alt = -FlightGlobals.getAltitudeAtPos(_sunCatchers[0].position, vessel.mainBody);
        alt = (alt * 3.0 + part.maxDepth) * 0.25;

        if (alt < 0.5)
          alt = 0.5;

        var density = 1.0 / (1.0 + alt * part.vessel.mainBody.oceanDensity);

        if (part.submergedPortion < 1.0)
          realFlow *= UtilMath.LerpUnclamped(1.0, density, part.submergedPortion);
        else
          realFlow *= density;

        SolarPanels += _localizedUnderwaterString;
      }

      if (_blockedParts >= _sunCatchers.Length)
        SolarPanels += string.Format(", {0}", Localizer.Format("#autoLOC_234994", _obscuringPart));

      if (_blockedBodies >= _sunCatchers.Length)
        SolarPanels = Localizer.Format("#autoLOC_234994", _obscuringBody);

      EnergyFlow = (float) (resHandler.UpdateModuleResourceOutputs(realFlow, 0) * realFlow);
    }

    private bool PartLOS(Transform refXForm, out string obscuringPart)
    {
      var sunVisible = true;
      obscuringPart = "nil";

      var sun = FlightGlobals.Bodies[0];

      if (Physics.Raycast(refXForm.position, refXForm.position - sun.transform.position, out var hit, 2500f))
      {
        var hitObj = hit.transform;
        var pt = hitObj.GetComponent<Part>();
        if (pt != null && pt != part)
        {
          sunVisible = false;
          obscuringPart = pt.partInfo.name;
        }
      }

      return sunVisible;
    }

    private bool SolarLOS(Transform refXForm, out float angle, out string obscuringBody)
    {
      var sunVisible = true;
      angle = 0f;
      obscuringBody = "nil";

      var sun = FlightGlobals.Bodies[0];
      var currentBody = FlightGlobals.currentMainBody;

      if (DebugMode)
        DrawSunLines(refXForm, sun.bodyTransform, 2, Color.red);

      angle = Vector3.Angle(refXForm.forward, sun.transform.position - refXForm.position);

      if (currentBody != sun)
      {
        Vector3 vT = sun.position - part.vessel.GetWorldPos3D();
        Vector3 vC = currentBody.position - part.vessel.GetWorldPos3D();
        // if true, behind horizon plane
        if (Vector3.Dot(vT, vC) > vC.sqrMagnitude - currentBody.Radius * currentBody.Radius)
          // if true, obscured
          if (Mathf.Pow(Vector3.Dot(vT, vC), 2) / vT.sqrMagnitude >
              vC.sqrMagnitude - currentBody.Radius * currentBody.Radius)
          {
            sunVisible = false;
            obscuringBody = currentBody.name;
          }
      }

      return sunVisible;
    }

    protected override void onSwitch(int index, int selection, Part p)
    {
      if (!_switcher)
        return;

      if (SolarPanelsLocked)
        return;

      if (p != part)
        return;

      for (var i = _SwitchIndices.Length - 1; i >= 0; i--)
        if (_SwitchIndices[i] == index)
        {
          CurrentSelection = selection;

          UpdateSolarPanels();

          break;
        }
    }

    private void UpdateSolarPanels()
    {
      if (!IsActive && HighLogic.LoadedSceneIsFlight)
      {
        _sunExposureField.guiActive = false;
        _energyFlowField.guiActive = false;
        _solarPanelField.guiActive = false;

        IsActive = false;
        IsDeployed = false;
        _panelsAvailable = false;
      }
      else if (_chargeRates != null && _chargeRates.Length > CurrentSelection)
      {
        _currentChargeRate = _chargeRates[CurrentSelection];

        if (_sunCatchers != null)
          _panelChargeRate = _currentChargeRate / _sunCatchers.Length;

        _panelsAvailable = _currentChargeRate > 0;

        _sunExposureField.guiActive = _currentChargeRate > 0;
        _energyFlowField.guiActive = _currentChargeRate > 0;
        _solarPanelField.guiActive = _currentChargeRate > 0;
        _solarPanelCostField.guiActiveEditor = DisplayCurrentModeCost && _currentChargeRate > 0;

        _tglEvent.active = _currentChargeRate > 0;

        if (_currentChargeRate <= 0)
          SolarPanels = _localizedUnavailableString;
        else
          SolarPanels = IsActive ? _localizedActiveString : _localizedInactiveString;

        if (DebugMode)
          debug.debugMessage(string.Format("Set solar panel total charge rate: {0:N2}", _currentChargeRate));

        resHandler.outputResources.Clear();

        var resourceMod = new ModuleResource();
        resourceMod.name = resourceName;
        resourceMod.title = resourceName.PrintSpacedStringFromCamelcase();
        resourceMod.id = resourceName.GetHashCode();
        resourceMod.rate = _currentChargeRate;

        resHandler.outputResources.Add(resourceMod);
      }
      else
      {
        _sunExposureField.guiActive = false;
        _energyFlowField.guiActive = false;
        _solarPanelField.guiActive = false;
        _solarPanelField.guiActiveEditor = false;
        _solarPanelCostField.guiActiveEditor = false;

        IsActive = false;
        IsDeployed = false;
        _panelsAvailable = false;
      }
    }

    private void DrawSunLines(Transform source, Transform sun, float length, Color c)
    {
      var line = new GameObject("Sun Line");
      line.transform.position = source.position;
      var lr = line.AddComponent<LineRenderer>();
      lr.material = new Material(_DebugLineShader);
      lr.startColor = c;
      lr.endColor = c * 0.3f;
      lr.startWidth = 0.02f;
      lr.endWidth = 0.01f;

      var end = source.position + (sun.position - source.position).normalized * length;

      lr.SetPosition(0, source.position);
      lr.SetPosition(1, end);
      Destroy(line, TimeWarp.fixedDeltaTime);
    }

    private void DrawDebugLines(Transform[] sources, float length, Color c)
    {
      if (sources == null)
        return;

      for (var i = sources.Length - 1; i >= 0; i--)
      {
        var line = new GameObject("Debug Line");
        line.transform.position = sources[i].position;
        var lr = line.AddComponent<LineRenderer>();
        lr.material = new Material(_DebugLineShader);
        lr.startColor = c;
        lr.endColor = c * 0.3f;
        lr.startWidth = 0.03f;
        lr.endWidth = 0.01f;

        var end = sources[i].position + sources[i].forward * length;

        lr.SetPosition(0, sources[i].position);
        lr.SetPosition(1, end);
        Destroy(line, TimeWarp.deltaTime);
      }
    }

    public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
    {
      if (SolarPanelsLocked || !IsActive)
        return 0;

      float cost = 0;

      if (_Costs != null && _Costs.Length >= CurrentSelection)
        cost = _Costs[CurrentSelection];

      AddedCostValue = cost;

      return cost;
    }

    public ModifierChangeWhen GetModuleCostChangeWhen()
    {
      return ModifierChangeWhen.CONSTANTLY;
    }
  }
}