using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text.Json;
using ArchiSteamFarm.Plugins.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TradeManagerPlus.Tests;

[TestClass]
public sealed class PluginConfigTests {
	[TestMethod]
	public void PluginIsExportedWithTheIPluginContract() {
		ExportAttribute? export = typeof(TradeManagerPlusPlugin).GetCustomAttributes(typeof(ExportAttribute), inherit: false).OfType<ExportAttribute>().SingleOrDefault();

		Assert.IsNotNull(export);
		Assert.AreEqual(typeof(IPlugin), export!.ContractType);
	}

	[TestMethod]
	public void MinimumDecimalValueOffsetCanBeFormattedWithoutOverflow() {
		Assert.AreEqual(" - 79228162514264337593543950335", TradeManagerPlusPlugin.FormatValueOffset(decimal.MinValue));
		Assert.AreEqual(" + 12.5", TradeManagerPlusPlugin.FormatValueOffset(12.5m));
		Assert.AreEqual(" - 12.5", TradeManagerPlusPlugin.FormatValueOffset(-12.5m));
	}

	[TestMethod]
	public void TopLevelConfigIsFound() {
		Dictionary<string, JsonElement> properties = ParseProperties("""{ "TradeManagerPlus": { "Enabled": false } }""");

		JsonElement? config = TradeManagerPlusPlugin.GetPluginConfigElement(properties);

		Assert.IsNotNull(config);
		Assert.IsFalse(config.Value.GetProperty("Enabled").GetBoolean());
	}

	[TestMethod]
	public void PluginNameLookupIsCaseInsensitive() {
		Dictionary<string, JsonElement> properties = ParseProperties("""{ "trademanagerplus": { "Enabled": false } }""");

		JsonElement? config = TradeManagerPlusPlugin.GetPluginConfigElement(properties);

		Assert.IsNotNull(config);
		Assert.IsFalse(config.Value.GetProperty("Enabled").GetBoolean());
	}

	[TestMethod]
	public void LegacyPluginsWrapperIsFound() {
		Dictionary<string, JsonElement> properties = ParseProperties("""{ "Plugins": { "TradeManagerPlus": { "Enabled": false } } }""");

		JsonElement? config = TradeManagerPlusPlugin.GetPluginConfigElement(properties);

		Assert.IsNotNull(config);
		Assert.IsFalse(config.Value.GetProperty("Enabled").GetBoolean());
	}

	[TestMethod]
	public void MissingConfigReturnsNull() {
		Dictionary<string, JsonElement> properties = ParseProperties("""{ "SteamLogin": "example" }""");

		Assert.IsNull(TradeManagerPlusPlugin.GetPluginConfigElement(properties));
		Assert.IsNull(TradeManagerPlusPlugin.GetPluginConfigElement(null));
	}

	private static Dictionary<string, JsonElement> ParseProperties(string json) {
		using JsonDocument document = JsonDocument.Parse(json);

		return document.RootElement.EnumerateObject().ToDictionary(static property => property.Name, static property => property.Value.Clone());
	}
}
