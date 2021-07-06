﻿using System;
using KSP.Localization;

namespace UniversalStorage2
{
  public class USMassSwitch : USBaseSwitch, IPartMassModifier
  {
    [KSPField] public string AddedMass = string.Empty;
    [KSPField] public bool DisplayCurrentModeMass = false;
    [KSPField] public string DisplayMassName = "Part Mass";

    [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Part Mass")]
    public float DryMassInfo = 0f;

    [KSPField(isPersistant = true)] public int CurrentSelection = 0;

    private double[] _Masses;
    private EventData<int, Part, USFuelSwitch> onFuelRequestMass;
    private bool _updateMass = true;

    private string _localizedDryMassString = "Part Mass";

    public override void OnAwake()
    {
      base.OnAwake();

      onFuelRequestMass = GameEvents.FindEvent<EventData<int, Part, USFuelSwitch>>("onFuelRequestMass");

      if (onFuelRequestMass != null)
        onFuelRequestMass.Add(onFuelSwitchRequest);

      _localizedDryMassString = Localizer.Format(DisplayMassName);

      Fields["DryMassInfo"].guiName = _localizedDryMassString;
    }

    public override void OnStart(StartState state)
    {
      base.OnStart(state);

      if (string.IsNullOrEmpty(AddedMass))
        return;

      _Masses = USTools.parseDoubles(AddedMass).ToArray();

      Fields["DryMassInfo"].guiActiveEditor = DisplayCurrentModeMass;
    }

    private void OnDestroy()
    {
      if (onFuelRequestMass != null)
        onFuelRequestMass.Remove(onFuelSwitchRequest);
    }

    protected override void onSwitch(int index, int selection, Part p)
    {
      if (p != part)
        return;

      if (_SwitchIndices == null || _SwitchIndices.Length <= 0)
      {
        if (string.IsNullOrEmpty(SwitchID))
          return;

        _SwitchIndices = USTools.parseIntegers(SwitchID).ToArray();
      }

      for (var i = _SwitchIndices.Length - 1; i >= 0; i--)
        if (_SwitchIndices[i] == index)
        {
          CurrentSelection = selection;

          break;
        }
    }

    private void onFuelSwitchRequest(int index, Part p, USFuelSwitch fuel)
    {
      _updateMass = false;

      if (p != part)
        return;

      if (fuel == null || fuel.part != p)
        return;

      float mass = 0;

      if (_Masses == null || _Masses.Length <= 0)
      {
        if (string.IsNullOrEmpty(AddedMass))
          return;

        _Masses = USTools.parseDoubles(AddedMass).ToArray();
      }

      if (_Masses.Length > CurrentSelection)
        mass = (float) _Masses[CurrentSelection];

      fuel.setMeshMass(mass);
    }

    private float UpdateWeight(Part currentPart)
    {
      float mass = 0;

      if (_Masses != null && _Masses.Length >= CurrentSelection)
        mass = (float) _Masses[CurrentSelection];

      DryMassInfo = currentPart.partInfo.partPrefab.mass + mass;

      return mass;
    }

    public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
    {
      if (!_updateMass)
        return 0;

      return UpdateWeight(part);
    }

    public ModifierChangeWhen GetModuleMassChangeWhen()
    {
      return ModifierChangeWhen.CONSTANTLY;
    }
  }
}