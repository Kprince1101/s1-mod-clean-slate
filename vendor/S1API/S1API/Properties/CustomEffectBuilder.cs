#if (IL2CPPMELON)
using S1Properties = Il2CppScheduleOne.Effects;
using S1Product = Il2CppScheduleOne.Product;
#elif (MONOMELON || MONOBEPINEX || IL2CPPBEPINEX)
using S1Properties = ScheduleOne.Effects;
using S1Product = ScheduleOne.Product;
#endif
using System;
using System.Collections.Generic;
using S1API.Entities;
using S1API.Internal.Properties;
using S1API.Logging;
using S1API.Products;
using UnityEngine;

namespace S1API.Properties
{
    /// <summary>
    /// Builder for composing a custom effect (drug property) at runtime.
    /// Configure the effect with fluent methods, then call <see cref="Build"/> to register it.
    /// </summary>
    /// <remarks>
    /// A custom effect is a real game effect that can be imprinted onto products by a mixing ingredient.
    /// Its display metadata, value contribution, and mixing geometry are configurable. Optional behavior runs
    /// through the same effect-callback path S1API already uses for overriding vanilla effects.
    /// </remarks>
    public sealed class CustomEffectBuilder
    {
        private static readonly Log Logger = new Log("CustomEffectBuilder");

        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private int _tier = 1;
        private float _addictiveness = 0.1f;
        private Color _productColor = Color.white;
        private Color _labelColor = Color.white;
        private int _valueChange;
        private float _valueMultiplier = 1f;
        private float _addBaseValueMultiple;
        private Vector2 _mixDirection = Vector2.zero;
        private float _mixMagnitude = 1f;
        private Vector2? _mixMapPosition;
        private float _mixMapRadius;
        private DrugType[]? _drugs;
        private Action<Player>? _playerBehavior;
        private Action<NPC>? _npcBehavior;

        /// <summary>
        /// Sets the effect's identity and display text.
        /// </summary>
        /// <param name="id">Unique effect ID (e.g. "mymod_glow"). Must be unique across all effects.</param>
        /// <param name="name">Display name shown on products.</param>
        /// <param name="description">Description shown in tooltips.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithBasicInfo(string id, string name, string description)
        {
            _id = id ?? string.Empty;
            _name = name ?? string.Empty;
            _description = description ?? string.Empty;
            return this;
        }

        /// <summary>
        /// Sets the effect tier (1-5), which affects its ordering and value contribution.
        /// </summary>
        /// <param name="tier">Tier from 1 to 5.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithTier(int tier)
        {
            _tier = Mathf.Clamp(tier, 1, 5);
            return this;
        }

        /// <summary>
        /// Sets how addictive this effect makes a product (0 to 1).
        /// </summary>
        /// <param name="addictiveness">Addictiveness from 0 to 1.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithAddictiveness(float addictiveness)
        {
            _addictiveness = Mathf.Clamp01(addictiveness);
            return this;
        }

        /// <summary>
        /// Sets how this effect changes a product's value.
        /// </summary>
        /// <param name="valueChange">Flat value added (-100 to 100).</param>
        /// <param name="valueMultiplier">Multiplier applied to the product value (0 to 2).</param>
        /// <param name="addBaseValueMultiple">Fraction of the base value added (-1 to 1).</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithValue(int valueChange, float valueMultiplier = 1f, float addBaseValueMultiple = 0f)
        {
            _valueChange = Mathf.Clamp(valueChange, -100, 100);
            _valueMultiplier = Mathf.Clamp(valueMultiplier, 0f, 2f);
            _addBaseValueMultiple = Mathf.Clamp(addBaseValueMultiple, -1f, 1f);
            return this;
        }

        /// <summary>
        /// Sets the colors used for this effect on the product and its label.
        /// </summary>
        /// <param name="productColor">The color tint applied to the product.</param>
        /// <param name="labelColor">The color used for the effect label.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithColors(Color productColor, Color labelColor)
        {
            _productColor = productColor;
            _labelColor = labelColor;
            return this;
        }

        /// <summary>
        /// Sets the mixing geometry used when this effect is the one being mixed into a product.
        /// </summary>
        /// <param name="direction">Direction on the mixing plane.</param>
        /// <param name="magnitude">Magnitude of the mixing vector.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithMixGeometry(Vector2 direction, float magnitude)
        {
            _mixDirection = direction;
            _mixMagnitude = magnitude;
            return this;
        }

        /// <summary>
        /// Overrides where this effect sits on each drug's mixing map. By default the effect is placed off to
        /// the side so it does not capture other mixes; set this to design deliberate reactions.
        /// </summary>
        /// <param name="position">Position on the mixing plane.</param>
        /// <param name="radius">Radius of the effect's region.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithMixMapPlacement(Vector2 position, float radius)
        {
            _mixMapPosition = position;
            _mixMapRadius = radius;
            return this;
        }

        /// <summary>
        /// Restricts which drugs this effect participates in mixing for. Defaults to all mixable drugs.
        /// </summary>
        /// <param name="drugs">The drug types to register the effect for.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder ForDrugs(params DrugType[] drugs)
        {
            _drugs = drugs;
            return this;
        }

        /// <summary>
        /// Sets the behavior run when this effect is applied to the local player.
        /// </summary>
        /// <param name="onApply">Callback invoked with the local player when the effect triggers.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithBehavior(Action<Player> onApply)
        {
            _playerBehavior = onApply;
            return this;
        }

        /// <summary>
        /// Sets the behavior run when this effect is applied to an NPC.
        /// </summary>
        /// <param name="onApply">Callback invoked with the NPC when the effect triggers.</param>
        /// <returns>The builder instance for fluent chaining.</returns>
        public CustomEffectBuilder WithNpcBehavior(Action<NPC> onApply)
        {
            _npcBehavior = onApply;
            return this;
        }

        /// <summary>
        /// Builds and registers the custom effect and returns a token usable anywhere a property is accepted.
        /// </summary>
        /// <returns>A <see cref="CustomEffect"/> token for the created effect.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no ID was set.</exception>
        public CustomEffect Build()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Logger.Error("Cannot build custom effect: ID is required. Use WithBasicInfo(...) to set the ID.");
                throw new InvalidOperationException("Cannot build custom effect: ID is required.");
            }

            // Building the same ID again (e.g. from a per-load setup hook) reuses the first registration, so the
            // game's ID lookups keep resolving to a single effect object instead of a later, uninjected one.
            if (CustomEffectRegistry.TryGetExisting(_id, out var existing))
            {
                Logger.Warning($"Custom effect '{_id}' is already registered; returning the existing effect.");
                return new CustomEffect(existing);
            }

            // A pure no-op vanilla effect is used as the carrier type: it never applies or clears any state,
            // so the effect only does what its registered behavior does. The carrier type is never checked by
            // the game; effects are matched by ID and object reference.
            S1Properties.Effect effect = ScriptableObject.CreateInstance<S1Properties.Refreshing>();
            effect.name = _id;
            effect.ID = _id;
            effect.Name = string.IsNullOrEmpty(_name) ? _id : _name;
            effect.Description = _description;
            effect.Tier = _tier;
            effect.Addictiveness = _addictiveness;
            effect.ProductColor = _productColor;
            effect.LabelColor = _labelColor;
            effect.ValueChange = _valueChange;
            effect.ValueMultiplier = _valueMultiplier;
            effect.AddBaseValueMultiple = _addBaseValueMultiple;
            effect.MixDirection = _mixDirection;
            effect.MixMagnitude = _mixMagnitude;
            // Mark the effect as available on all saves, including those created before the mixing rework;
            // otherwise the game filters it out of tier-based property lookups on older saves.
            effect.ImplementedPriorMixingRework = true;

            if (_playerBehavior != null)
                ProductManager.SetEffectCallback(_id, _playerBehavior, allowDefaultEffect: false);

            if (_npcBehavior != null)
                ProductManager.SetNpcEffectCallback(_id, _npcBehavior, allowDefaultEffect: false);

            CustomEffectRegistry.Register(effect, ResolveDrugs(), _mixMapPosition, _mixMapRadius);

            return new CustomEffect(effect);
        }

        private S1Product.EDrugType[] ResolveDrugs()
        {
            var drugs = _drugs != null && _drugs.Length > 0
                ? _drugs
                : new[] { DrugType.Marijuana, DrugType.Methamphetamine, DrugType.Cocaine, DrugType.Shrooms };

            var native = new List<S1Product.EDrugType>(drugs.Length);
            foreach (var drug in drugs)
                native.Add((S1Product.EDrugType)drug);

            return native.ToArray();
        }
    }
}
