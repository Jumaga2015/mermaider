using System.Globalization;
using System.Xml.Linq;
using AwesomeAssertions;

namespace Mermaider.Tests;

/// <summary>
/// Vertical order of flowchart nodes inside nested subgraphs, checked against the layers dagre 3.1.4 (the layout engine
/// of mermaid.js) computes for the same graph. The fixture is an anonymised copy of a real diagram with the same
/// structure: 21 nodes, 24 edges, 8 subgraphs (3 top-level, 5 nested), 19 edges that cross a subgraph border.
/// </summary>
public class CompoundSubgraphRankTests
{
	private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

	private const string Fixture =
		"""
		flowchart TB
		    subgraph SRV_A["Server A (zone one)"]
		        subgraph SCH_A1["Schema A1"]
		            T1[("TABLE_A1_AGGREGATE<br/>TABLE")]
		            T2[("TABLE_A1_DESCRIPTION<br/>TABLE")]
		            T3[("TABLE_A1_PERIOD<br/>TABLE")]
		        end
		        subgraph SCH_B1["Schema B1"]
		            P1["PROC_B1_META<br/>PROCEDURE"]
		            P2["PROC_B1_AGGREGATE<br/>PROCEDURE"]
		            S1["SYN_A1_*<br/>SYNONYMS → A1"]
		            S2["SYN_B1_*<br/>SYNONYMS → SRV_B@LNK"]
		        end
		    end

		    subgraph SRV_B["Server B (zone two)"]
		        subgraph SCH_A2["Schema A2"]
		            TW1[("TABLE_A2_AGGREGATE<br/>TABLE")]
		            TW2[("TABLE_A2_PERIOD<br/>TABLE")]
		            PW1["PROC_A2_AGGREGATE<br/>PROCEDURE"]
		        end
		        subgraph SCH_C2["Schema C2"]
		            E1[("TABLE_C2_AGGREGATE<br/>TABLE")]
		            E2[("TABLE_C2_DESCRIPTION<br/>TABLE")]
		            E3[("VIEW_C2_*<br/>VIEWS")]
		            E4[("VIEW_C2_LEVELS<br/>VIEW")]
		        end
		        subgraph SCH_B2["Schema B2"]
		            SW["VIS_C2_*<br/>SYNONYMS → C2"]
		        end
		    end

		    subgraph APPS["Applications"]
		        APP_X["Application X<br/>(source)"]
		        APP_Y["Application Y<br/>(web)"]
		        APP_Z["Process Z<br/>(transfer)"]
		        APP_W["Portal W<br/>(website)"]
		        RPT_1["Reports<br/>(site one)"]
		        RPT_2["Reports<br/>(site two)"]
		    end

		    APP_X -->|"Step 1"| T1
		    APP_X --> T2
		    APP_X --> T3

		    T1 --> S1
		    S1 --> P1
		    S1 --> P2
		    P1 -->|"Step 3a"| S2
		    P2 -->|"Step 3b"| S2
		    S2 -->|"@SRV_B.LNK"| TW1
		    S2 -->|"@SRV_B.LNK"| TW2

		    APP_Y -->|"triggers"| P1
		    APP_Y -->|"triggers"| P2

		    TW1 --> PW1
		    PW1 -->|"Step 4"| E1

		    APP_Z -->|"triggers"| PW1

		    E1 --> SW
		    E2 --> SW
		    E3 --> SW
		    SW --> APP_W

		    TW1 --> RPT_1
		    TW2 --> RPT_1
		    E1 --> RPT_2
		    E3 --> RPT_2
		    E4 --> RPT_2
		""";

	/// <summary>
	/// Ordinal layers from dagre 3.1.4 for <see cref="Fixture"/>: nodes on the same row share a layer, and a lower
	/// number is higher on the page.
	/// </summary>
	private static readonly IReadOnlyDictionary<string, int> DagreLayer = new Dictionary<string, int>
	{
		["APP_X"] = 0,
		["T1"] = 1, ["T2"] = 1, ["T3"] = 1,
		["S1"] = 2, ["APP_Y"] = 2,
		["P1"] = 3, ["P2"] = 3,
		["S2"] = 4,
		["TW1"] = 5, ["TW2"] = 5, ["APP_Z"] = 5,
		["PW1"] = 6, ["RPT_1"] = 6,
		["E1"] = 7, ["E2"] = 7, ["E3"] = 7, ["E4"] = 7,
		["SW"] = 8, ["RPT_2"] = 8,
		["APP_W"] = 9,
	};

	private static readonly string[] AppNodes = ["APP_X", "APP_Y", "APP_Z", "APP_W", "RPT_1", "RPT_2"];

	[Test]
	public void Nodes_are_ordered_vertically_as_dagre_orders_them()
	{
		var centres = NodeCentres(MermaidRenderer.RenderSvg(Fixture));

		var violations = new List<string>();
		foreach (var upper in DagreLayer.Keys)
		{
			foreach (var lower in DagreLayer.Keys)
			{
				if (DagreLayer[upper] < DagreLayer[lower] && centres[upper] >= centres[lower])
				{
					violations.Add($"{upper} should be above {lower}");
				}
			}
		}

		violations.Count.Should().Be(0, $"dagre places each layer above the next ones; inverted pairs: {string.Join("; ", violations)}");
	}

	[Test]
	public void Application_nodes_do_not_share_one_row()
	{
		var centres = NodeCentres(MermaidRenderer.RenderSvg(Fixture));

		var rows = AppNodes.Select(id => Math.Round(centres[id])).Distinct().Count();

		rows.Should().BeGreaterThan(1, "the application nodes have edges to different layers in dagre");
	}

	/// <summary>Vertical centre of each node, by <c>data-id</c>, from the first rectangle of its group.</summary>
	private static Dictionary<string, double> NodeCentres(string svg)
	{
		var doc = XDocument.Parse(svg);
		var centres = new Dictionary<string, double>();
		foreach (var node in doc.Descendants(Svg + "g").Where(g => (string?)g.Attribute("class") == "node"))
		{
			var rect = node.Descendants(Svg + "rect").First();
			var y = double.Parse((string)rect.Attribute("y")!, CultureInfo.InvariantCulture);
			var height = double.Parse((string)rect.Attribute("height")!, CultureInfo.InvariantCulture);
			centres[(string)node.Attribute("data-id")!] = y + height / 2;
		}

		return centres;
	}
}
