using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TradeManagerPlus.Tests;

[TestClass]
public sealed class PluginConfigTests {
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
