using Blish_HUD;
using Quarry.Interfaces;
using System;
using System.Linq;
using System.Reflection;

namespace Quarry.Services
{
    // Phase 16: the only reflection into Pathing in the module -- lifted from the deleted
    // PathingProbeService's GetPackInitiator/GetProp, same guards and once-per-session logging.
    // Target chain (verified against docs/PATHING-PR.md and a local clone of the Pathing module's source):
    // PathingModule.PackInitiator -> .PackState -> .CategoryStates -> SetInactive(string, bool) /
    // GetNamespaceInactive(string). Nothing else in the module may reflect into Pathing.
    public class PathingBridge : IPathingBridge
    {
        private const string PathingNamespace = "bh.community.pathing";

        private readonly Logger logger;

        private object cachedModuleInstance;
        private object cachedCategoryStates;
        private MethodInfo getNamespaceInactiveMethod;
        private MethodInfo setInactiveMethod;
        private bool loggedMissing;
        private bool loggedShapeMismatch;

        public PathingBridge(Logger logger)
        {
            this.logger = logger;
        }

        public bool IsAvailable => this.ResolveCategoryStates() != null;

        public bool TryGetInactive(string categoryNamespace, out bool inactive)
        {
            inactive = false;

            var categoryStates = this.ResolveCategoryStates();
            if (categoryStates is null)
            {
                return false;
            }

            try
            {
                inactive = (bool)this.getNamespaceInactiveMethod.Invoke(categoryStates, new object[] { categoryNamespace });
                return true;
            }
            catch (Exception ex)
            {
                this.LogShapeMismatchOnce(ex);
                return false;
            }
        }

        public bool TrySetInactive(string categoryNamespace, bool inactive)
        {
            var categoryStates = this.ResolveCategoryStates();
            if (categoryStates is null)
            {
                return false;
            }

            try
            {
                _ = this.setInactiveMethod.Invoke(categoryStates, new object[] { categoryNamespace, inactive });
                return true;
            }
            catch (Exception ex)
            {
                this.LogShapeMismatchOnce(ex);
                return false;
            }
        }

        // Caches the resolved MethodInfos after the first success; re-resolves if Pathing's
        // ModuleInstance changes (disabled/re-enabled), same as the probe's attach lifecycle.
        private object ResolveCategoryStates()
        {
            var moduleInstance = GetPathingModuleInstance(this.logger, ref this.loggedMissing);
            if (moduleInstance is null)
            {
                this.cachedModuleInstance = null;
                this.cachedCategoryStates = null;
                return null;
            }

            if (ReferenceEquals(moduleInstance, this.cachedModuleInstance) && this.cachedCategoryStates != null)
            {
                return this.cachedCategoryStates;
            }

            this.cachedModuleInstance = moduleInstance;
            this.cachedCategoryStates = null;
            this.getNamespaceInactiveMethod = null;
            this.setInactiveMethod = null;

            var packInitiator = GetProp(moduleInstance, "PackInitiator");
            var packState = GetProp(packInitiator, "PackState");
            var categoryStates = GetProp(packState, "CategoryStates");

            if (categoryStates is null)
            {
                if (!this.loggedMissing)
                {
                    this.loggedMissing = true;
                    this.logger.Info("Pathing bridge: Pathing loaded but PackInitiator/PackState/CategoryStates isn't ready yet; will retry.");
                }

                return null;
            }

            var categoryStatesType = categoryStates.GetType();
            this.getNamespaceInactiveMethod = categoryStatesType.GetMethod("GetNamespaceInactive", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
            this.setInactiveMethod = categoryStatesType.GetMethod("SetInactive", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string), typeof(bool) }, null);

            if (this.getNamespaceInactiveMethod is null || this.setInactiveMethod is null)
            {
                this.LogShapeMismatchOnce(null);
                return null;
            }

            this.cachedCategoryStates = categoryStates;
            return this.cachedCategoryStates;
        }

        private static object GetPathingModuleInstance(Logger logger, ref bool loggedMissing)
        {
            try
            {
                var pathing = GameService.Module.Modules.FirstOrDefault(m => string.Equals(m.Manifest?.Namespace, PathingNamespace, StringComparison.OrdinalIgnoreCase));
                if (pathing is null || !pathing.Enabled || pathing.ModuleInstance is null)
                {
                    if (!loggedMissing)
                    {
                        loggedMissing = true;
                        logger.Info(pathing is null
                            ? "Pathing bridge: Pathing module not installed; hunt mode inactive."
                            : "Pathing bridge: Pathing module installed but not enabled/loaded yet; will retry.");
                    }

                    return null;
                }

                return pathing.ModuleInstance;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Pathing bridge: failed to locate Pathing module.");
                return null;
            }
        }

        private static object GetProp(object target, string name)
        {
            if (target is null)
            {
                return null;
            }

            var prop = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return prop?.GetValue(target);
        }

        private void LogShapeMismatchOnce(Exception ex)
        {
            if (this.loggedShapeMismatch)
            {
                return;
            }

            // Error, not Warn (2.0.4), so it reaches Sentry: Pathing changed its internals and hunt mode is
            // dead for everyone running both modules. Once per session, so it can't flood.
            this.loggedShapeMismatch = true;
            this.logger.Error(ex, "Pathing bridge: Pathing's CategoryStates shape didn't match what we expect; hunt mode will do nothing until this is fixed.");
        }
    }
}
