namespace UniversalStorage2.StockVariants
{
  public struct USVariantInfo
  {
    private string _variantType;
    private string _displayName;
    private string _primaryColor;
    private string _secondaryColor;

    public string VariantType => _variantType;

    public string DisplayName => _displayName;

    public string PrimaryColor => _primaryColor;

    public string SecondaryColor => _secondaryColor;

    public USVariantInfo(string typeName, string name, string primary, string secondary)
    {
      _variantType = typeName;
      _displayName = name;
      _primaryColor = primary;
      _secondaryColor = secondary;
    }
  }
}