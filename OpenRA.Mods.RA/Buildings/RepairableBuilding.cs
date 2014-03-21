#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.RA.Effects;
using OpenRA.Traits;
using OpenRA.FileFormats;

namespace OpenRA.Mods.RA.Buildings
{
	[Desc("Building can be repaired by the repair button.")]
	public class RepairableBuildingInfo : ITraitInfo, Requires<HealthInfo>
	{
		public readonly int RepairPercent = 20;
		public readonly int RepairInterval = 24;
		public readonly int RepairStep = 7;
		public readonly int MaxPlayerRepair = 3;
		public readonly int ExtraRepairPercent = 10;
		public readonly string IndicatorPalettePrefix = "player";

		public object Create(ActorInitializer init) { return new RepairableBuilding(init.self, this); }
	}

	public class RepairableBuilding : ITick, ISync
	{
		private List<Player> Repairers;

		Health Health;
		RepairableBuildingInfo Info;

		public RepairableBuilding(Actor self, RepairableBuildingInfo info)
		{
			Health = self.Trait<Health>();
			Repairers = new List<Player>();
			Info = info;
		}

		public bool isPlayerRepairing(Player p)
		{
			return Repairers.Contains(p);
		}

		public void RepairBuilding(Actor self, Player p)
		{
			if (self.HasTrait<RepairableBuilding>())
			{
				if (self.AppearsFriendlyTo(p.PlayerActor) && Repairers.Count < Info.MaxPlayerRepair)
				{
					if (Repairers.Contains(p))
						Repairers.Remove(p);

					else
					{
						Repairers.Add(p);
						Sound.PlayNotification(p, "Speech", "Repairing", self.Owner.Country.Race);

						self.World.AddFrameEndTask(
							w => w.Add(new RepairIndicator(self, Info.IndicatorPalettePrefix, p)));
					}
				}
			}
		}

		int remainingTicks;

		public void Tick(Actor self)
		{
			if (Repairers.Count == 0) return;

			if (remainingTicks == 0)
			{
				foreach (var p in Repairers)
					if (p.WinState != WinState.Undefined || p.Stances[self.Owner] != Stance.Ally) Repairers.Remove(p);

				var buildingValue = self.GetSellValue();

				var hpToRepair = Math.Min(Info.RepairStep, Health.MaxHP - Health.HP);
				var cost = Math.Max(1, (hpToRepair * Info.RepairPercent  * buildingValue) / (Health.MaxHP * 100));
				// if any players can't afford the cost then they are done repairing
				foreach (var p in Repairers)
				{
					if (!p.PlayerActor.Trait<PlayerResources>().TakeCash(cost))
					{
						Repairers.Remove(p);

						// if no players can afford the cost
						if (Repairers.Count < 1)
						{
							remainingTicks = 1;
							return;
						}
					}
				}

				// repair extra percentage of hp for every additional player repairing the building
				var extra = hpToRepair * (Info.ExtraRepairPercent / 100) * (Repairers.Count - 1);
				hpToRepair = Math.Min(Info.RepairStep + extra, Health.MaxHP - Health.HP);
				self.InflictDamage(self, -hpToRepair, null);

				if (Health.DamageState == DamageState.Undamaged)
				{
					Repairers.Clear();
					return;
				}

				remainingTicks = Info.RepairInterval;
			}
			else
				--remainingTicks;
		}
	}
}
