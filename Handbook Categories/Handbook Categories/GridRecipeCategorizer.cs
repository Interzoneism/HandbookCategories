using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Handbook_Categories
{
    public static class GridRecipeCategorizer
    {
        private static readonly string[] CategoryOrder =
        {
            "Armor",
            "Clothes",
            "Tools",
            "Weapons",
            "Storage",
            "Furniture",
            "Consumables",
            "Machinery",
            "Transportation",
            "Components",
            "Construction",
            "Tech",
        };

        public static IEnumerable<string> AllCategories => CategoryOrder;

        private static readonly Dictionary<string, string> ManualRecipeCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["anchor.json"] = "Transportation",
            ["antlermount.json"] = "Furniture",
            ["armor/antique.json"] = "Armor",
            ["armor/brigandine.json"] = "Armor",
            ["armor/chain.json"] = "Armor",
            ["armor/gambeson.json"] = "Armor",
            ["armor/hide.json"] = "Armor",
            ["armor/improvisedwoodarmor.json"] = "Armor",
            ["armor/jerkin.json"] = "Armor",
            ["armor/lamellar.json"] = "Armor",
            ["armor/leather.json"] = "Armor",
            ["armor/plate.json"] = "Armor",
            ["armor/scale.json"] = "Armor",
            ["armor/tailoredgambeson.json"] = "Armor",
            ["armorrepair/antique.json"] = "Armor",
            ["armorrepair/brigandine.json"] = "Armor",
            ["armorrepair/chain.json"] = "Armor",
            ["armorrepair/gambeson.json"] = "Armor",
            ["armorrepair/improvisedwoodarmor.json"] = "Armor",
            ["armorrepair/jerkin.json"] = "Armor",
            ["armorrepair/lamellar.json"] = "Armor",
            ["armorrepair/leather.json"] = "Armor",
            ["armorrepair/plate.json"] = "Armor",
            ["armorrepair/scale.json"] = "Armor",
            ["armorrepair/tailoredgambeson.json"] = "Armor",
            ["armorstand.json"] = "Furniture",
            ["backpack.json"] = "Storage",
            ["bambooshoot.json"] = "Consumables",
            ["bandage.json"] = "Consumables",
            ["barrel.json"] = "Storage",
            ["basket-deconstruct.json"] = "Storage",
            ["basket.json"] = "Storage",
            ["baskettrap.json"] = "Weapons",
            ["beams.json"] = "Construction",
            ["bed.json"] = "Furniture",
            ["beenade.json"] = "Weapons",
            ["blastingpowder.json"] = "Weapons",
            ["bloomerybase.json"] = "Construction",
            ["bloomerychimney.json"] = "Construction",
            ["boat-seat.json"] = "Transportation",
            ["bomb.json"] = "Weapons",
            ["books.json"] = "Furniture",
            ["bookshelf.json"] = "Furniture",
            ["butterflynet.json"] = "Tools",
            ["cassava.json"] = "Consumables",
            ["chair.json"] = "Furniture",
            ["chest-labeled.json"] = "Storage",
            ["chest.json"] = "Storage",
            ["chimney.json"] = "Construction",
            ["chiseledblockcombine.json"] = "Construction",
            ["churn.json"] = "Machinery",
            ["chute.json"] = "Machinery",
            ["clay.json"] = "Construction",
            ["claybrickblock.json"] = "Construction",
            ["clayshinglesblock.json"] = "Construction",
            ["cloth.json"] = "Components",
            ["clothes/black.json"] = "Clothes",
            ["clothes/blue.json"] = "Clothes",
            ["clothes/brown.json"] = "Clothes",
            ["clothes/green.json"] = "Clothes",
            ["clothes/hat.json"] = "Clothes",
            ["clothes/hide.json"] = "Clothes",
            ["clothes/leather.json"] = "Clothes",
            ["clothes/necklace.json"] = "Clothes",
            ["clothes/orange.json"] = "Clothes",
            ["clothes/pink.json"] = "Clothes",
            ["clothes/plain.json"] = "Clothes",
            ["clothes/purple.json"] = "Clothes",
            ["clothes/red.json"] = "Clothes",
            ["cob.json"] = "Construction",
            ["cobblestone.json"] = "Construction",
            ["condenser.json"] = "Machinery",
            ["crank.json"] = "Machinery",
            ["crate-labeled.json"] = "Storage",
            ["crate-opened.json"] = "Storage",
            ["crate.json"] = "Storage",
            ["crystalsmash/amethyst.json"] = "Construction",
            ["crystalsmash/milkyquartz.json"] = "Construction",
            ["crystalsmash/olivine.json"] = "Construction",
            ["crystalsmash/rosequartz.json"] = "Construction",
            ["crystalsmash/smokyquartz.json"] = "Construction",
            ["daub-raw.json"] = "Construction",
            ["debarkedlog.json"] = "Construction",
            ["diamondtile.json"] = "Construction",
            ["displaycase.json"] = "Storage",
            ["door-metal.json"] = "Construction",
            ["doors.json"] = "Construction",
            ["doublechest.json"] = "Storage",
            ["dough.json"] = "Consumables",
            ["drygrass.json"] = "Components",
            ["drypackeddirt.json"] = "Construction",
            ["drystone.json"] = "Construction",
            ["drystonefence.json"] = "Construction",
            ["figurehead.json"] = "Transportation",
            ["firestarter.json"] = "Tools",
            ["firewood.json"] = "Consumables",
            ["forge.json"] = "Machinery",
            ["fruitpress.json"] = "Machinery",
            ["glasspane.json"] = "Construction",
            ["glider.json"] = "Transportation",
            ["haybale.json"] = "Construction",
            ["helvehammer.json"] = "Machinery",
            ["henbox.json"] = "Storage",
            ["hide-pelt-divide.json"] = "Components",
            ["hide-salted.json"] = "Components",
            ["hopper.json"] = "Machinery",
            ["hunterbackpack.json"] = "Storage",
            ["ingot.json"] = "Components",
            ["inkandquill.json"] = "Components",
            ["instrument.json"] = "Furniture",
            ["jonas/nightvisiondevice.json"] = "Tech",
            ["jonas/resonator.json"] = "Tech",
            ["jonas/returnbaserecipes.json"] = "Tech",
            ["jonas/returndeath.json"] = "Tech",
            ["jonas/riftward.json"] = "Tech",
            ["ladder.json"] = "Construction",
            ["lantern.json"] = "Tech",
            ["lightningrod.json"] = "Construction",
            ["linen.json"] = "Components",
            ["linensack.json"] = "Storage",
            ["log-quad.json"] = "Construction",
            ["mechpowerblocks-pulverizer.json"] = "Machinery",
            ["mechpowerblocks.json"] = "Machinery",
            ["metalbit-jewelryscrap.json"] = "Components",
            ["metalbit.json"] = "Components",
            ["metalblock.json"] = "Construction",
            ["metalplaque.json"] = "Construction",
            ["miningbag.json"] = "Storage",
            ["moldrack.json"] = "Storage",
            ["mudbricks.json"] = "Construction",
            ["nuggets.json"] = "Components",
            ["oiledhide.json"] = "Components",
            ["oillamp.json"] = "Tech",
            ["packeddirt.json"] = "Construction",
            ["pan.json"] = "Tools",
            ["parchment.json"] = "Components",
            ["pemmican.json"] = "Consumables",
            ["pineappleslice.json"] = "Consumables",
            ["plank.json"] = "Construction",
            ["planks.json"] = "Construction",
            ["plaster/diagonal.json"] = "Construction",
            ["plaster/plain.json"] = "Construction",
            ["plaster/square.json"] = "Construction",
            ["plaster/stripes.json"] = "Construction",
            ["plumbandsquare.json"] = "Tools",
            ["polishedrock.json"] = "Construction",
            ["poultice.json"] = "Consumables",
            ["prospectingpick.json"] = "Tools",
            ["pumpkinseed.json"] = "Consumables",
            ["pumpkinslice.json"] = "Consumables",
            ["quartzglass.json"] = "Construction",
            ["quern.json"] = "Machinery",
            ["raft.json"] = "Transportation",
            ["rammedearth.json"] = "Construction",
            ["ratlines.json"] = "Transportation",
            ["rawbrick.json"] = "Construction",
            ["rawrefractorybrick.json"] = "Construction",
            ["refractorybrickblock.json"] = "Construction",
            ["refractorygratingblock.json"] = "Construction",
            ["roller.json"] = "Machinery",
            ["roofing.json"] = "Construction",
            ["rope.json"] = "Components",
            ["roughhewnfence.json"] = "Construction",
            ["roughhewngate.json"] = "Construction",
            ["rushmat.json"] = "Furniture",
            ["schematiccopy.json"] = "Tech",
            ["scrapedhide.json"] = "Components",
            ["scrapweaponkit.json"] = "Weapons",
            ["scrollrack.json"] = "Storage",
            ["sealedcrock.json"] = "Storage",
            ["sewingkit.json"] = "Components",
            ["shelf.json"] = "Storage",
            ["sieve.json"] = "Machinery",
            ["sign.json"] = "Construction",
            ["signpost.json"] = "Construction",
            ["skep.json"] = "Storage",
            ["slabmode/claybrick.json"] = "Construction",
            ["slabmode/cobble.json"] = "Construction",
            ["slabmode/glass.json"] = "Construction",
            ["slabmode/mudbrick.json"] = "Construction",
            ["slabmode/planks.json"] = "Construction",
            ["slabmode/polishedrock.json"] = "Construction",
            ["slabmode/quartz.json"] = "Construction",
            ["slabmode/shingle.json"] = "Construction",
            ["slabmode/stonebrick.json"] = "Construction",
            ["slabs/claybrickslab.json"] = "Construction",
            ["slabs/clayshingleslab.json"] = "Construction",
            ["slabs/cobbleslabs.json"] = "Construction",
            ["slabs/glassslabs.json"] = "Construction",
            ["slabs/mudbrickslab.json"] = "Construction",
            ["slabs/plankslabs.json"] = "Construction",
            ["slabs/polishedrockslab.json"] = "Construction",
            ["slabs/stonebrickslab.json"] = "Construction",
            ["slabs/stonepathslab.json"] = "Transportation",
            ["snowballvariants.json"] = "Weapons",
            ["soil-compost.json"] = "Construction",
            ["solderbar.json"] = "Components",
            ["stackedbamboo.json"] = "Construction",
            ["stairs/claybrickstairs.json"] = "Construction",
            ["stairs/clayshinglestairs.json"] = "Construction",
            ["stairs/cobblestairs.json"] = "Construction",
            ["stairs/plankstairs.json"] = "Construction",
            ["stairs/stonebrickstairs.json"] = "Construction",
            ["stairs/stonepathstairs.json"] = "Transportation",
            ["stationarybasket-deconstruct.json"] = "Storage",
            ["stationarybasket.json"] = "Storage",
            ["stickslayer.json"] = "Weapons",
            ["stonebrick.json"] = "Construction",
            ["stonebricks.json"] = "Construction",
            ["stonecoffin.json"] = "Furniture",
            ["stonecoffinlid.json"] = "Furniture",
            ["stonepath.json"] = "Transportation",
            ["strawbedding.json"] = "Furniture",
            ["strawdummy.json"] = "Furniture",
            ["table.json"] = "Furniture",
            ["tack.json"] = "Transportation",
            ["tobtranslocator.json"] = "Tech",
            ["tool/arrow.json"] = "Weapons",
            ["tool/axe.json"] = "Tools",
            ["tool/blade.json"] = "Weapons",
            ["tool/bow.json"] = "Weapons",
            ["tool/bowstave.json"] = "Weapons",
            ["tool/club.json"] = "Weapons",
            ["tool/hackingspear.json"] = "Weapons",
            ["tool/hammer.json"] = "Tools",
            ["tool/hoe.json"] = "Tools",
            ["tool/knife.json"] = "Tools",
            ["tool/pickaxe.json"] = "Tools",
            ["tool/saw.json"] = "Tools",
            ["tool/scythe.json"] = "Tools",
            ["tool/shield.json"] = "Weapons",
            ["tool/shovel.json"] = "Tools",
            ["tool/sling.json"] = "Weapons",
            ["tool/solderingiron.json"] = "Tech",
            ["tool/spear.json"] = "Weapons",
            ["tool/tongs.json"] = "Tools",
            ["toolrack.json"] = "Storage",
            ["torch.json"] = "Consumables",
            ["torchholder.json"] = "Furniture",
            ["trap-crate.json"] = "Weapons",
            ["trapdoor.json"] = "Construction",
            ["trough-large.json"] = "Storage",
            ["trough-small.json"] = "Storage",
            ["twine.json"] = "Components",
            ["verticalboiler.json"] = "Machinery",
            ["wattlefence.json"] = "Construction",
            ["waxedcheese.json"] = "Consumables",
            ["wildvine.json"] = "Construction",
            ["woodbucket.json"] = "Storage",
            ["woodenfence.json"] = "Construction",
            ["woodenfencegate.json"] = "Construction",
            ["woodenpath.json"] = "Transportation",
        };

        public static string Categorize(GridRecipe recipe)
        {
            if (recipe == null)
            {
                throw new ArgumentNullException(nameof(recipe));
            }

            if (TryGetManualCategory(recipe, out string category))
            {
                return category;
            }

            return "Construction";
        }

        private static bool TryGetManualCategory(GridRecipe recipe, out string category)
        {
            category = null;

            AssetLocation name = recipe.Name;
            if (name == null)
            {
                return false;
            }

            string path = name.Path;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string normalized = NormalizePath(path);
            if (normalized.Length > 0 && ManualRecipeCategories.TryGetValue(normalized, out category))
            {
                return true;
            }

            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex >= 0)
            {
                string fileName = normalized.Substring(slashIndex + 1);
                if (ManualRecipeCategories.TryGetValue(fileName, out category))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            string normalized = path.Replace("\\", "/");

            const string prefix = "recipes/grid/";
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(prefix.Length);
            }

            if (!normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".json";
            }

            return normalized;
        }
    }
}
