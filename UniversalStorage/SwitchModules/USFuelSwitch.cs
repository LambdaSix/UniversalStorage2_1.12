using System;
using System.Collections.Generic;
using System.Text;
using KSP.Localization;

namespace UniversalStorage2
{
  public class USFuelSwitch : PartModule, IPartCostModifier, IPartMassModifier
  {
    [KSPField] public string SwitchID = string.Empty;

    [KSPField] public string resourceNames =
      "ElectricCharge;ElectricCharge|LiquidFuel,Oxidizer;LiquidFuel,Oxidizer|MonoPropellant;MonoPropellant|Structural;Structural";

    [KSPField] public string resourceAmounts = "100;100|75,25;75,25|200;200|0;0";
    [KSPField] public string initialResourceAmounts = "100;100|75,25;75,25|200;200|0;0";
    [KSPField] public string tankMass = "0;0|0;0|0;0|0;0";
    [KSPField] public string tankCost = "0;0|0;0|0;0|0;0";
    [KSPField] public bool displayCurrentTankCost = true;
    [KSPField] public bool displayCurrentTankDryMass = true;
    [KSPField] public bool availableInFlight = false;
    [KSPField] public bool availableInEditor = false;
    [KSPField] public bool ShowInfo = true;
    [KSPField(isPersistant = true)] public int selectedTankModeOne = -1;
    [KSPField(isPersistant = true)] public int selectedTankModeTwo = -1;
    [KSPField(isPersistant = true)] public bool hasLaunched = false;
    [KSPField(isPersistant = true)] public bool configLoaded = false;
    [KSPField] public bool DebugMode = false;
    [KSPField] public string DisplayCostName = "Dry Cost";
    [KSPField] public string DisplayMassName = "Dry Mass";
    [KSPField] public string ModuleDisplayName = "Fuel Switch";

    [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Dry cost")]
    public float addedCost = 0f;

    [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Dry mass")]
    public float dryMassInfo = 0f;

    private List<List<List<USResource>>> tankList;

    private int[] _SwitchIndices;

    private float meshCost = 0;
    private float meshMass = 0;

    private List<List<double>> weightList;
    private List<List<double>> tankCostList;

    private bool initialized = false;

    private UIPartActionWindow tweakableUI;

    private USdebugMessages debug;

    private EventData<int, int, bool, Part> onUSFuelSwitch;
    private EventData<int, Part, USFuelSwitch> onFuelRequestMass;
    private EventData<int, Part, USFuelSwitch> onFuelRequestCost;

    private string _localizedDryCostString = "Dry Cost";
    private string _localizedDryMassString = "Dry Mass";

    public override void OnStart(StartState state)
    {
      if (string.IsNullOrEmpty(SwitchID))
        return;

      _SwitchIndices = USTools.parseIntegers(SwitchID).ToArray();

      onFuelRequestCost = GameEvents.FindEvent<EventData<int, Part, USFuelSwitch>>("onFuelRequestCost");
      onFuelRequestMass = GameEvents.FindEvent<EventData<int, Part, USFuelSwitch>>("onFuelRequestMass");
      onUSFuelSwitch = GameEvents.FindEvent<EventData<int, int, bool, Part>>("onUSFuelSwitch");

      _localizedDryCostString = Localizer.Format(DisplayCostName);
      _localizedDryMassString = Localizer.Format(DisplayMassName);

      Fields["addedCost"].guiName = _localizedDryCostString;
      Fields["dryMassInfo"].guiName = _localizedDryMassString;

      if (onUSFuelSwitch != null)
        onUSFuelSwitch.Add(OnFuelSwitch);

      initializeData();

      if (selectedTankModeOne == -1 || selectedTankModeTwo == -1)
      {
        selectedTankModeOne = 0;
        selectedTankModeTwo = 0;
        assignResourcesToPart(false);
      }

      onFuelRequestMass.Fire(_SwitchIndices[0], part, this);
      onFuelRequestCost.Fire(_SwitchIndices[0], part, this);
    }

    public override void OnAwake()
    {
      if (configLoaded)
        initializeData();
    }

    private void OnDestroy()
    {
      if (onUSFuelSwitch != null)
        onUSFuelSwitch.Remove(OnFuelSwitch);
    }

    public override void OnLoad(ConfigNode node)
    {
      base.OnLoad(node);

      if (!configLoaded)
        initializeData();

      configLoaded = true;
    }

    public override string GetInfo()
    {
      if (ShowInfo)
      {
        var resourceList = USTools.parseNames(resourceNames);

        var info = StringBuilderCache.Acquire();
        info.AppendLine(Localizer.Format("#autoLOC_US_FuelVariants"));

        for (var i = 0; i < resourceList.Count; i++)
          info.AppendLine(resourceList[i].Replace(",", ", "));

        return info.ToStringAndRelease();
      }
      else
      {
        return base.GetInfo();
      }
    }

    public override string GetModuleDisplayName()
    {
      return Localizer.Format(ModuleDisplayName);
    }

    private void initializeData()
    {
      if (!initialized)
      {
        debug = new USdebugMessages(DebugMode, "USFuelSwitch");

        setupTankList();

        weightList = new List<List<double>>();

        var weights = tankMass.Split('|');

        for (var i = 0; i < weights.Length; i++) weightList.Add(USTools.parseDoubles(weights[i]));

        tankCostList = new List<List<double>>();

        var costs = tankCost.Split('|');

        for (var i = 0; i < costs.Length; i++) tankCostList.Add(USTools.parseDoubles(costs[i]));

        if (HighLogic.LoadedSceneIsFlight)
          hasLaunched = true;

        Events["nextTankSetupEvent"].guiActive = availableInFlight;
        Events["nextTankSetupEvent"].guiActiveEditor = availableInEditor;
        Events["previousTankSetupEvent"].guiActive = availableInFlight;
        Events["previousTankSetupEvent"].guiActiveEditor = availableInEditor;
        Events["nextModeEvent"].guiActive = availableInFlight;
        Events["nextModeEvent"].guiActiveEditor = availableInEditor;
        Events["previousModeEvent"].guiActive = availableInFlight;
        Events["previousModeEvent"].guiActiveEditor = availableInEditor;

        if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
          Fields["addedCost"].guiActiveEditor = displayCurrentTankCost;

        Fields["dryMassInfo"].guiActiveEditor = displayCurrentTankDryMass;

        initialized = true;
      }
    }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Next tank mode setup")]
    public void nextModeEvent()
    {
      selectedTankModeTwo++;

      if (selectedTankModeTwo >= tankList.Count)
        selectedTankModeTwo = 0;

      assignResourcesToPart(true);
    }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Previous tank mode setup")]
    public void previousModeEvent()
    {
      selectedTankModeTwo--;

      if (selectedTankModeTwo < 0)
        selectedTankModeTwo = tankList.Count - 1;

      assignResourcesToPart(true);
    }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Next tank setup")]
    public void nextTankSetupEvent()
    {
      selectedTankModeOne++;

      if (selectedTankModeOne >= tankList[selectedTankModeTwo].Count)
        selectedTankModeOne = 0;

      assignResourcesToPart(true);
    }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Previous tank setup")]
    public void previousTankSetupEvent()
    {
      selectedTankModeOne--;

      if (selectedTankModeOne < 0)
        selectedTankModeOne = tankList[selectedTankModeTwo].Count - 1;

      assignResourcesToPart(true);
    }

    private void OnFuelSwitch(int index, int selection, bool modeOne, Part p)
    {
      if (p != part)
        return;

      for (var i = _SwitchIndices.Length - 1; i >= 0; i--)
        if (_SwitchIndices[i] == index)
        {
          if (modeOne)
          {
            selectedTankModeOne = selection;

            if (onFuelRequestCost != null)
              onFuelRequestCost.Fire(index, part, this);

            if (onFuelRequestMass != null)
              onFuelRequestMass.Fire(index, part, this);

            assignResourcesToPart(true);
          }
          else
          {
            selectedTankModeTwo = selection;

            if (onFuelRequestCost != null)
              onFuelRequestCost.Fire(index, part, this);

            if (onFuelRequestMass != null)
              onFuelRequestMass.Fire(index, part, this);

            assignResourcesToPart(true);
          }

          break;
        }
    }

    public void setMeshCost(float cost)
    {
      meshCost = cost;
    }

    public void setMeshMass(float mass)
    {
      meshMass = mass;
    }

    private void assignResourcesToPart(bool calledByPlayer)
    {
      // destroying a resource messes up the gui in editor, but not in flight.
      setupTankInPart(part, calledByPlayer);

      if (HighLogic.LoadedSceneIsEditor)
        for (var s = 0; s < part.symmetryCounterparts.Count; s++)
        {
          setupTankInPart(part.symmetryCounterparts[s], calledByPlayer);

          var symSwitch = part.symmetryCounterparts[s].GetComponent<USFuelSwitch>();

          if (symSwitch != null)
          {
            symSwitch.selectedTankModeOne = selectedTankModeOne;
            symSwitch.selectedTankModeTwo = selectedTankModeTwo;
          }
        }

      if (tweakableUI == null)
        tweakableUI = USTools.FindActionWindow(part);

      if (tweakableUI != null)
        tweakableUI.displayDirty = true;
      else
        debug.debugMessage("no UI to refresh");
    }

    private void setupTankInPart(Part currentPart, bool calledByPlayer)
    {
      currentPart.Resources.dict = new DictionaryValueList<int, PartResource>();
      var partResources = currentPart.GetComponents<PartResource>();

      for (var i = 0; i < tankList.Count; i++)
      {
        if (DebugMode)
          debug.debugMessage(string.Format("Tank Mode: {0} - Selection: {1}", i, selectedTankModeTwo));

        if (selectedTankModeTwo == i)
          for (var j = 0; j < tankList[i].Count; j++)
          {
            if (DebugMode)
              debug.debugMessage(string.Format("Tank: {0} - Selection: {1}", j, selectedTankModeOne));

            if (selectedTankModeOne == j)
              for (var k = 0; k < tankList[i][j].Count; k++)
              {
                var res = tankList[i][j][k];
                if (res.name != "Structural")
                {
                  var newResourceNode = new ConfigNode("RESOURCE");
                  newResourceNode.AddValue("name", res.name);
                  newResourceNode.AddValue("maxAmount", res.maxAmount);

                  if (calledByPlayer && !HighLogic.LoadedSceneIsEditor)
                    newResourceNode.AddValue("amount", 0.0f);
                  else
                    newResourceNode.AddValue("amount", res.amount);

                  if (DebugMode)
                    debug.debugMessage(string.Format("Switch to new resource: {0} - Amount: {1:N2} - Max: {2:N2}",
                      res.name, res.amount, res.maxAmount));

                  currentPart.AddResource(newResourceNode);
                }
              }
          }
      }

      updateWeight(currentPart);
      updateCost();
    }

    private void setupTankList()
    {
      tankList = new List<List<List<USResource>>>();

      var resourceList = new List<List<List<double>>>();
      var initialResourceList = new List<List<List<double>>>();

      var resourceModeArray = resourceAmounts.Split('|');
      var initialResourceModeArray = initialResourceAmounts.Split('|');

      if (string.IsNullOrEmpty(initialResourceAmounts) || initialResourceModeArray.Length != resourceModeArray.Length)
        initialResourceModeArray = resourceModeArray;

      for (var i = 0; i < resourceModeArray.Length; i++)
      {
        var resourceTankArray = resourceModeArray[i].Split(';');
        var initialResourceTankArray = initialResourceModeArray[i].Split(';');

        if (string.IsNullOrEmpty(initialResourceModeArray[i]) ||
            initialResourceTankArray.Length != resourceTankArray.Length)
          initialResourceTankArray = resourceTankArray;

        var resourceTankAmounts = new List<List<double>>();
        var initResourceTankAmounts = new List<List<double>>();

        for (var j = 0; j < resourceTankArray.Length; j++)
        {
          var resourceAmountArray = resourceTankArray[j].Trim().Split(',');
          var initialResourceAmountArray = initialResourceTankArray[j].Trim().Split(',');

          if (string.IsNullOrEmpty(initialResourceTankArray[j]) ||
              initialResourceAmountArray.Length != resourceAmountArray.Length)
            initialResourceAmountArray = resourceAmountArray;

          var resList = new List<double>();
          var initList = new List<double>();

          for (var k = 0; k < resourceAmountArray.Length; k++)
          {
            double res = 0;
            double init = 0;

            double.TryParse(resourceAmountArray[k].Trim(), out res);
            resList.Add(res);

            double.TryParse(initialResourceAmountArray[k].Trim(), out init);
            initList.Add(init);
          }

          resourceTankAmounts.Add(resList);
          initResourceTankAmounts.Add(initList);
        }

        resourceList.Add(resourceTankAmounts);
        initialResourceList.Add(initResourceTankAmounts);
      }

      // Then find the kinds of resources each tank holds, and fill them with the amounts found previously, or the amount they held last (values kept in save persistence/craft)
      var modeArray = resourceNames.Split('|');

      for (var i = 0; i < modeArray.Length; i++)
      {
        var newTankModes = new List<List<USResource>>();

        var mode = modeArray[i];

        var tankArray = mode.Split(';');

        for (var j = 0; j < tankArray.Length; j++)
        {
          var newResources = new List<USResource>();

          var resourceArray = tankArray[j].Split(',');

          for (var k = 0; k < resourceArray.Length; k++)
          {
            var res = resourceArray[k].Trim(' ');

            var newResource = new USResource()
            {
              name = res
            };

            if (resourceList != null && i < resourceList.Count)
              if (resourceList[i] != null && j < resourceList[i].Count)
                if (resourceList[i][j] != null && k < resourceList[i][j].Count)
                  newResource.maxAmount = resourceList[i][j][k];

            if (initialResourceList != null && i < initialResourceList.Count)
              if (initialResourceList[i] != null && j < initialResourceList[i].Count)
                if (initialResourceList[i][j] != null && k < initialResourceList[i][j].Count)
                  newResource.amount = initialResourceList[i][j][k];

            newResources.Add(newResource);
          }

          newTankModes.Add(newResources);
        }

        tankList.Add(newTankModes);
      }
    }

    private float updateCost()
    {
      float cost = 0;

      if (selectedTankModeTwo >= 0 && selectedTankModeTwo < tankCostList.Count)
        if (selectedTankModeOne >= 0 && selectedTankModeOne < tankCostList[selectedTankModeTwo].Count)
          cost = (float) tankCostList[selectedTankModeTwo][selectedTankModeOne];

      var newCost = cost + meshCost;

      addedCost = getDryCost(part.partInfo.cost + newCost);

      return newCost;
    }

    private float getDryCost(float fullCost)
    {
      var cost = fullCost;

      for (var i = part.Resources.Count - 1; i >= 0; i--)
      {
        var res = part.Resources[i];
        var def = res.info;

        cost -= def.unitCost * (float) res.maxAmount;
      }

      return cost;
    }

    private float updateWeight(Part currentPart)
    {
      float mass = 0;

      if (selectedTankModeTwo >= 0 && selectedTankModeTwo < weightList.Count)
        if (selectedTankModeOne >= 0 && selectedTankModeOne < weightList[selectedTankModeTwo].Count)
          mass = (float) weightList[selectedTankModeTwo][selectedTankModeOne];

      var newMass = mass + meshMass;

      dryMassInfo = currentPart.partInfo.partPrefab.mass + newMass;

      return newMass;
    }

    public float GetModuleCost(float defaultCost, ModifierStagingSituation sit)
    {
      return updateCost();
    }

    public ModifierChangeWhen GetModuleCostChangeWhen()
    {
      return ModifierChangeWhen.CONSTANTLY;
    }

    public float GetModuleMass(float defaultMass, ModifierStagingSituation sit)
    {
      return updateWeight(part);
    }

    public ModifierChangeWhen GetModuleMassChangeWhen()
    {
      return ModifierChangeWhen.CONSTANTLY;
    }
  }
}