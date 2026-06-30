using System;
using UnityEngine;

namespace ReactionTactics.Grid
{
    /// <summary>
    /// Scene-level owner for the active tactical grid map and grid-to-world metrics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Serialized map definition used to build the runtime tactical grid.")]
        private GridMapDefinition mapDefinition;

        [SerializeField]
        [Tooltip("Conversion settings shared by grid terrain, units, and picking.")]
        private GridMetrics gridMetrics = GridMetrics.Default;

        public GridMapDefinition MapDefinition
        {
            get { return mapDefinition; }
        }

        public GridMetrics Metrics
        {
            get { return gridMetrics; }
        }

        public IGridMap CurrentMap { get; private set; }

        public bool HasCurrentMap
        {
            get { return CurrentMap != null; }
        }

        /// <summary>
        /// Assigns the map definition used by this manager and optionally rebuilds the runtime map immediately.
        /// </summary>
        public bool SetMapDefinition(GridMapDefinition mapDefinition, bool rebuild = true)
        {
            this.mapDefinition = mapDefinition;
            if (!rebuild)
            {
                CurrentMap = null;
                return true;
            }

            return RebuildMap();
        }

        public bool RebuildMap()
        {
            if (mapDefinition == null)
            {
                CurrentMap = null;
                Debug.LogError(
                    $"{nameof(GridManager)} on '{name}' cannot build {nameof(CurrentMap)} because no {nameof(GridMapDefinition)} is assigned.",
                    this);
                return false;
            }

            try
            {
                CurrentMap = mapDefinition.BuildGridMap();
                return true;
            }
            catch (Exception exception)
            {
                CurrentMap = null;
                Debug.LogError(
                    $"{nameof(GridManager)} on '{name}' failed to build {nameof(CurrentMap)} from map definition '{mapDefinition.name}': {exception.Message}",
                    this);
                return false;
            }
        }

        private void Awake()
        {
            RebuildMap();
        }

        private void Reset()
        {
            gridMetrics = GridMetrics.Default;
        }
    }
}
