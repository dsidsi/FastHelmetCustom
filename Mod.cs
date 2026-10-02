using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json;

namespace FastHelmetCustom;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.custom.fasthelmet";
    public override string Name { get; init; } = "FAST Gen2 Helmets";
    public override string Author { get; init; } = "Kimi";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.1.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; } = false;
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 10)]
public class FastHelmetCustomMod(
    ISptLogger<FastHelmetCustomMod> logger,
    DatabaseService databaseService,
    JsonUtil jsonUtil) : IOnLoad
{
    // 原版模板
    private const string TanTpl = "5ac8d6885acfc400180ae7b0";
    private const string BlackTpl = "5a154d5cfcdbcb001a3b00da";
    private const string VisorTpl = "5a16b672fcdbcb001912fa83";
    private const string SideArmorTpl = "5a16badafcdbcb001865f72d";
    private const string MandibleTpl = "5a16ba61fcdbcb098008728a";
    private const string SlaapTpl = "5c0e66e2d174af02a96252f4";
    private const string PlateTopTpl = "657f8ec5f4c82973640b234c";
    private const string PlateBackTpl = "657f8f10f4c82973640b2350";
    private const string SlotPrototype = "55d30c4c4bdc2db4468b457e";
    private const string DollarsTpl = "5696686a4bdc2da3298b456a";

    private record ItemDef(
        string NewId, string OfferId, string SrcTpl, string NewName,
        string ZhName, string EnName, string ZhDesc, string EnDesc,
        int Price,
        string? ArmorClass = null, bool HalvePenalty = false, bool Sell = true,
        string BundleOverride = "",
        string? ExtraFilterSlot = null, string? ExtraFilterTpl = null);

    private record HelmetDef(
        string NewId, string SrcTpl, string NewName,
        string SlotId, string OfferId, string ChildTopId, string ChildBackId,
        string ZhName, string EnName, string ZhDesc, string EnDesc, int Price);

    // ---- Gen2 配件 ----
    private static readonly ItemDef[] Accessories =
    [
        new("66b1c2a3f4e5d60718293b1a", "66b1c2a3f4e5d60718293c1a", VisorTpl, "item_equipment_helmet_fast_gen2_visor",
            "FAST Gen2 面罩", "FAST Gen2 visor",
            "FAST Gen2 复合面罩，5 级防护，光学畸变与重量惩罚已优化。", "FAST Gen2 composite visor, class 5, optimized optics and weight penalties.",
            320, ArmorClass: "5", HalvePenalty: true),
        new("66b1c2a3f4e5d60718293b1b", "66b1c2a3f4e5d60718293c1b", SideArmorTpl, "item_equipment_helmet_fast_gen2_side_armor",
            "FAST Gen2 侧甲", "FAST Gen2 side armor",
            "FAST Gen2 耳侧装甲，5 级防护，自带护颚挂点，佩戴更舒适。", "FAST Gen2 ear-side armor, class 5, integrated mandible mount, improved ergonomics.",
            300, ArmorClass: "5", HalvePenalty: true,
            ExtraFilterSlot: "mod_equipment", ExtraFilterTpl: "66b1c2a3f4e5d60718293b1c"),
        new("66b1c2a3f4e5d60718293b1c", "66b1c2a3f4e5d60718293c1c", MandibleTpl, "item_equipment_helmet_fast_gen2_mandible",
            "FAST Gen2 护颚", "FAST Gen2 mandible",
            "FAST Gen2 下颌护板，5 级防护，几乎不影响人机工效。", "FAST Gen2 mandible guard, class 5, minimal ergonomics impact.",
            280, ArmorClass: "5", HalvePenalty: true),
        new("66b1c2a3f4e5d60718293b1d", "66b1c2a3f4e5d60718293c1d", SlaapTpl, "item_equipment_helmet_fast_gen2_top_plate",
            "FAST Gen2 顶板", "FAST Gen2 top plate",
            "FAST Gen2 头顶附加装甲，6 级防护，轻量化设计。", "FAST Gen2 top add-on plate, class 6, lightweight design.",
            520, ArmorClass: "6", HalvePenalty: true),
        new("66b1c2a3f4e5d60718293b1e", "66b1c2a3f4e5d60718293c1e", PlateTopTpl, "item_equipment_helmet_fast_gen2_plate_top",
            "FAST Gen2 顶部装甲板", "FAST Gen2 top helmet plate",
            "FAST Gen2 内置顶部装甲板，5 级防护。", "FAST Gen2 integrated top plate, class 5.",
            200, ArmorClass: "5", HalvePenalty: true, Sell: false),
        new("66b1c2a3f4e5d60718293b1f", "66b1c2a3f4e5d60718293c1f", PlateBackTpl, "item_equipment_helmet_fast_gen2_plate_back",
            "FAST Gen2 后颈装甲板", "FAST Gen2 nape plate",
            "FAST Gen2 内置后颈装甲板，5 级防护。", "FAST Gen2 integrated nape plate, class 5.",
            170, ArmorClass: "5", HalvePenalty: true, Sell: false),
    ];

    private const string VisorGen2 = "66b1c2a3f4e5d60718293b1a";
    private const string SideGen2 = "66b1c2a3f4e5d60718293b1b";
    private const string MandibleGen2 = "66b1c2a3f4e5d60718293b1c";
    private const string TopPlateGen2 = "66b1c2a3f4e5d60718293b1d";
    private const string PlateTopGen2 = "66b1c2a3f4e5d60718293b1e";
    private const string PlateBackGen2 = "66b1c2a3f4e5d60718293b1f";

    // ---- Gen2 头盔 ----
    private static readonly HelmetDef[] Helmets =
    [
        new("66b1c2a3f4e5d60718293a4b", TanTpl, "item_equipment_helmet_fast_gen2_tan",
            "66b1c2a3f4e5d60718293a5b", "66b1c2a3f4e5d60718293a6b",
            "66b1c2a3f4e5d60718293a7b", "66b1c2a3f4e5d60718293a7c",
            "FAST MT Gen2 战术头盔（沙色）", "FAST MT Gen2 tactical helmet (Tan)",
            "FAST MT 二代定制头盔（沙色），5 级防护，可同时安装面罩、护颚、侧甲与顶板，基础操控惩罚已优化。",
            "Custom 2nd-gen FAST MT (tan), class 5, fits visor, mandible, side armor and top plate at the same time, with reduced base penalties.",
            850),
        new("66b1c2a3f4e5d60718293a4c", BlackTpl, "item_equipment_helmet_fast_gen2_black",
            "66b1c2a3f4e5d60718293a5c", "66b1c2a3f4e5d60718293a6c",
            "66b1c2a3f4e5d60718293a7d", "66b1c2a3f4e5d60718293a7e",
            "FAST MT Gen2 战术头盔（黑色）", "FAST MT Gen2 tactical helmet (Black)",
            "FAST MT 二代定制头盔（黑色），5 级防护，可同时安装面罩、护颚、侧甲与顶板，基础操控惩罚已优化。",
            "Custom 2nd-gen FAST MT (black), class 5, fits visor, mandible, side armor and top plate at the same time, with reduced base penalties.",
            850),
    ];

    public Task OnLoad()
    {
        try
        {
            LoadInternal();
        }
        catch (Exception ex)
        {
            logger.Error($"[FastHelmetCustom] load failed: {ex}", ex);
            throw;
        }
        return Task.CompletedTask;
    }

    private void LoadInternal()
    {
        var items = databaseService.GetItems();
        var handbook = databaseService.GetHandbook();
        var prices = databaseService.GetPrices();
        var locales = databaseService.GetLocales().Global;
        var assort = databaseService.GetTrader(Traders.PEACEKEEPER).Assort;

        foreach (var def in Accessories)
        {
            CreateClone(items, handbook, prices, locales, assort, def, null);
        }
        foreach (var def in Helmets)
        {
            CreateHelmet(items, handbook, prices, locales, assort, def);
        }

        logger.Success($"[FastHelmetCustom] added {Helmets.Length} Gen2 helmets and {Accessories.Length} Gen2 accessories to Peacekeeper (LL4).", null!);
    }

    private void CreateHelmet(
        Dictionary<MongoId, TemplateItem> items, HandbookBase handbook,
        Dictionary<MongoId, double> prices, Dictionary<string, LazyLoad<Dictionary<string, string>>> locales,
        TraderAssort assort, HelmetDef def)
    {
        var clone = jsonUtil.Deserialize<TemplateItem>(jsonUtil.Serialize(items[def.SrcTpl]))!;
        clone.Id = def.NewId;
        clone.Name = def.NewName;
        clone.Properties.Name = def.NewName;
        clone.Properties.ShortName = def.NewName;
        clone.Properties.Description = def.NewName;
        // 模型走原版 bundle 路径（客户端插件负责补 mod_equipment_003 挂载点）
        var srcPrefab = items[def.SrcTpl].Properties.Prefab;
        clone.Properties.Prefab = new Prefab { Path = srcPrefab.Path, Rcid = "" };
        // 基础惩罚回调为原版的一半
        clone.Properties.MousePenalty = (int)Math.Round((clone.Properties.MousePenalty ?? 0) / 2.0);
        clone.Properties.WeaponErgonomicPenalty = (int)Math.Round((clone.Properties.WeaponErgonomicPenalty ?? 0) / 2.0);

        // 新增 mod_equipment_003（专用侧甲槽）
        var slots = clone.Properties.Slots.ToList();
        slots.Add(new Slot
        {
            Name = "mod_equipment_003",
            Id = def.SlotId,
            Parent = def.NewId,
            Required = false,
            MergeSlotWithChildren = false,
            Prototype = SlotPrototype,
            Properties = new SlotProperties
            {
                Filters = [new SlotFilter { Filter = [SideArmorTpl, SideGen2] }]
            }
        });
        clone.Properties.Slots = slots;

        // 槽位过滤器扩充 Gen2 配件；面罩槽不再接受侧甲（侧甲走专用槽）
        AddToSlotFilter(clone, "mod_equipment_000", VisorGen2);
        RemoveFromSlotFilter(clone, "mod_equipment_000", SideArmorTpl);
        AddToSlotFilter(clone, "mod_equipment_002", TopPlateGen2);
        AddToSlotFilter(clone, "Helmet_top", PlateTopGen2);
        AddToSlotFilter(clone, "Helmet_back", PlateBackGen2);

        items.Add(def.NewId, clone);
        AddHandbook(handbook, prices, def.NewId, def.SrcTpl, def.Price);
        AddLocale(locales, def.NewId, def.NewName, def.ZhName, def.EnName, def.ZhDesc, def.EnDesc);
        AddOffer(assort, def.OfferId, def.NewId, def.Price,
            (def.ChildTopId, PlateTopGen2, "Helmet_top"),
            (def.ChildBackId, PlateBackGen2, "Helmet_back"));
    }

    private void CreateClone(
        Dictionary<MongoId, TemplateItem> items, HandbookBase handbook,
        Dictionary<MongoId, double> prices, Dictionary<string, LazyLoad<Dictionary<string, string>>> locales,
        TraderAssort assort, ItemDef def, (string, string, string)[]? children)
    {
        var clone = jsonUtil.Deserialize<TemplateItem>(jsonUtil.Serialize(items[def.SrcTpl]))!;
        clone.Id = def.NewId;
        clone.Name = def.NewName;
        clone.Properties.Name = def.NewName;
        clone.Properties.ShortName = def.NewName;
        clone.Properties.Description = def.NewName;
        if (!string.IsNullOrEmpty(def.BundleOverride))
        {
            clone.Properties.Prefab = new Prefab { Path = def.BundleOverride, Rcid = "" };
        }
        if (def.ArmorClass is not null)
        {
            clone.Properties.ArmorClass = int.Parse(def.ArmorClass);
        }
        if (def.HalvePenalty)
        {
            clone.Properties.MousePenalty = (int)Math.Round((clone.Properties.MousePenalty ?? 0) / 2.0);
            clone.Properties.WeaponErgonomicPenalty = (int)Math.Round((clone.Properties.WeaponErgonomicPenalty ?? 0) / 2.0);
        }
        if (def.ExtraFilterSlot is not null && def.ExtraFilterTpl is not null)
        {
            AddToSlotFilter(clone, def.ExtraFilterSlot, def.ExtraFilterTpl);
        }
        items.Add(def.NewId, clone);
        AddHandbook(handbook, prices, def.NewId, def.SrcTpl, def.Price);
        AddLocale(locales, def.NewId, def.NewName, def.ZhName, def.EnName, def.ZhDesc, def.EnDesc);
        if (def.Sell)
        {
            AddOffer(assort, def.OfferId, def.NewId, def.Price);
        }
    }

    private static void AddToSlotFilter(TemplateItem clone, string slotName, params string[] tpls)
    {
        var slot = clone.Properties.Slots.FirstOrDefault(s => s.Name == slotName);
        var filters = slot?.Properties?.Filters?.ToList();
        if (filters is not { Count: > 0 }) return;
        foreach (var tpl in tpls)
        {
            if (!filters[0].Filter.Contains(tpl))
            {
                filters[0].Filter.Add(tpl);
            }
        }
        slot!.Properties.Filters = filters;
    }

    private static void RemoveFromSlotFilter(TemplateItem clone, string slotName, params string[] tpls)
    {
        var slot = clone.Properties.Slots.FirstOrDefault(s => s.Name == slotName);
        var filters = slot?.Properties?.Filters?.ToList();
        if (filters is not { Count: > 0 }) return;
        foreach (var tpl in tpls)
        {
            filters[0].Filter.RemoveWhere(id => id == tpl);
        }
        slot!.Properties.Filters = filters;
    }

    private static void AddHandbook(HandbookBase handbook, Dictionary<MongoId, double> prices, string newId, string srcTpl, int price)
    {
        var srcHb = handbook.Items.First(x => x.Id == srcTpl);
        handbook.Items.Add(new HandbookItem { Id = newId, ParentId = srcHb.ParentId, Price = price });
        prices[newId] = price;
    }

    private static void AddLocale(Dictionary<string, LazyLoad<Dictionary<string, string>>> locales,
        string newId, string newName, string zhName, string enName, string zhDesc, string enDesc)
    {
        foreach (var (lang, lazy) in locales)
        {
            var isZh = lang is "ch" or "cn" or "zh" || lang.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            var name = isZh ? zhName : enName;
            var desc = isZh ? zhDesc : enDesc;
            lazy.AddTransformer(dict =>
            {
                foreach (var keyBase in new[] { newName, newId })
                {
                    dict[$"{keyBase} Name"] = name;
                    dict[$"{keyBase} ShortName"] = name;
                    dict[$"{keyBase} Description"] = desc;
                }
                return dict;
            });
        }
    }

    private void AddOffer(TraderAssort assort, string offerId, string tpl, int price,
        params (string Id, string Tpl, string SlotId)[] children)
    {
        var donorItem = assort.Items[0];
        var itemNode = JsonNode.Parse(jsonUtil.Serialize(donorItem))!;
        itemNode["_id"] = offerId;
        itemNode["_tpl"] = tpl;
        assort.Items.Add((Item)jsonUtil.Deserialize(itemNode.ToJsonString(), donorItem.GetType())!);

        foreach (var (childId, childTpl, slotId) in children)
        {
            var childJson = $"{{\"_id\":\"{childId}\",\"_tpl\":\"{childTpl}\",\"parentId\":\"{offerId}\",\"slotId\":\"{slotId}\"}}";
            assort.Items.Add(jsonUtil.Deserialize<Item>(childJson)!);
        }

        var donorKey = assort.BarterScheme.Keys.First();
        var donorBarter = assort.BarterScheme[donorKey];
        var barterNode = JsonNode.Parse(jsonUtil.Serialize(donorBarter))!;
        barterNode[0]![0]!["count"] = price;
        barterNode[0]![0]!["_tpl"] = DollarsTpl;
        assort.BarterScheme[offerId] =
            (List<List<BarterScheme>>)jsonUtil.Deserialize(barterNode.ToJsonString(), donorBarter.GetType())!;

        assort.LoyalLevelItems[offerId] = 4;
    }
}
