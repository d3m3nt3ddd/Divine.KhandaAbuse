namespace Khanda_Abuse;

using Divine.Entity;
using Divine.Entity.Entities.Abilities.Items;
using Divine.Entity.Entities.Abilities.Items.Components;
using Divine.Entity.Entities.Units.Heroes;
using Divine.Game;
using Divine.Menu;
using Divine.Menu.EventArgs;
using Divine.Menu.Items;
using Divine.Service;
using Divine.Update;
using System;
using System.Linq;

internal sealed class Bootstrap : Bootstrapper
{
    private MenuSwitcher EnableSwitcher = null!;

    private float _blockDisassembleUntil = 0f;
    private bool _disassembledByScript = false;
    private float _khandaReadyAt = 0f;
    private float _nextActionTime = 0f;

    protected override void OnMainActivate()
    {
        Console.WriteLine("OnMainActivate: Khanda Abuse Loaded");

        var menu = MenuManager.AbuseMenu.AddMenu("Khanda Abuse")
            .SetImage("panorama/images/items/angels_demise_png.vtex_c")
            .SetTooltip("Disassemble Khanda into Phylactery if it is on cooldown");

        EnableSwitcher = menu.AddSwitcher("Enable", true);
    }

    protected override void OnMainDeactivate()
    {
        Console.WriteLine("OnMainDeactivate: Khanda Abuse Unloaded");
    }

    protected override void OnActivate()
    {
        EnableSwitcher.ValueChanged += OnEnableChanged;
        if (EnableSwitcher.Value)
        {
            UpdateManager.CreateIngameUpdate(15, OnIngameUpdate);
        }
    }

    protected override void OnDeactivate()
    {
        EnableSwitcher.ValueChanged -= OnEnableChanged;
        UpdateManager.DestroyIngameUpdate(OnIngameUpdate);
    }

    private void OnEnableChanged(MenuSwitcher sender, SwitcherChangedEventArgs e)
    {
        if (e.Value)
        {
            _blockDisassembleUntil = 0f;
            _disassembledByScript = false;
            _khandaReadyAt = 0f;
            _nextActionTime = 0f;
            UpdateManager.CreateIngameUpdate(15, OnIngameUpdate);
        }
        else
        {
            UpdateManager.DestroyIngameUpdate(OnIngameUpdate);
        }
    }

    private void OnIngameUpdate()
    {
        float currentTime = GameManager.RawGameTime;

        
        if (currentTime < _nextActionTime) return;

        var localHero = EntityManager.LocalHero;

        if (localHero is null || !localHero.IsAlive || localHero.Inventory is null)
        {
            _disassembledByScript = false;
            _khandaReadyAt = 0f;
            return;
        }

        if (localHero.Spellbook != null && localHero.Spellbook.Spells.Any(x => x.IsInAbilityPhase || x.IsChanneling))
        {
            return;
        }

        var khanda = localHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_angels_demise") as Item;
        var phylactery = localHero.Inventory.Items.FirstOrDefault(x => x.Name == "item_phylactery") as Item;

        if (khanda != null && khanda.IsValid)
        {
            
            _disassembledByScript = false;
            _khandaReadyAt = 0f;

            if (currentTime < _blockDisassembleUntil) return;
            if (khanda.Cooldown < 1.0f) return;

            if (GetOccupiedSlotsCount(localHero) >= 9) return;

            if (khanda.IsDisassemblable)
            {
                _khandaReadyAt = currentTime + khanda.Cooldown;
                _disassembledByScript = true;

                khanda.Disassemble();
                
                _nextActionTime = currentTime + 0.35f;
            }
            return;
        }

        if (phylactery != null && phylactery.IsValid)
        {
            if (!_disassembledByScript)
            {
                return;
            }

            if (currentTime >= _khandaReadyAt)
            {
                if (HasLockedComponents(localHero))
                {
                    ForceReassembleComponents(localHero);
                    
                    _nextActionTime = currentTime + 0.35f;
                }
                else
                {
                    _disassembledByScript = false;
                }
                return;
            }

            if (phylactery.Cooldown > 0f)
            {
                _blockDisassembleUntil = currentTime + phylactery.Cooldown;

                if (HasLockedComponents(localHero))
                {
                    ForceReassembleComponents(localHero);
                    _nextActionTime = currentTime + 0.35f;
                }
                else
                {
                    _disassembledByScript = false;
                }
            }
            return;
        }

        if (_disassembledByScript)
        {
            if (HasLockedComponents(localHero))
            {
                ForceReassembleComponents(localHero);
                _nextActionTime = currentTime + 0.35f;
            }
            else
            {
                _disassembledByScript = false;
            }
        }
    }

    private int GetOccupiedSlotsCount(Hero localHero)
    {
        int count = 0;
        for (int i = 0; i <= 8; i++)
        {
            if (localHero.Inventory.GetItem((ItemSlot)i) != null)
            {
                count++;
            }
        }
        return count;
    }

    private bool HasLockedComponents(Hero localHero)
    {
        if (localHero.Inventory is null) return false;

        
        return localHero.Inventory.Items.Any(x =>
            x != null && x.IsValid && (x.Name == "item_phylactery" || x.Name == "item_soul_booster") && x.IsCombineLocked);
    }

    private void ForceReassembleComponents(Hero localHero)
    {
        if (localHero.Inventory is null) return;

        
        var itemsToUnlock = localHero.Inventory.Items
            .Where(x => x != null && x.IsValid && (x.Name == "item_phylactery" || x.Name == "item_soul_booster"));

        foreach (var component in itemsToUnlock)
        {
            if (component is Item itemComp && itemComp.IsCombineLocked)
            {
                itemComp.CombineUnlock();
            }
        }
    }
}
