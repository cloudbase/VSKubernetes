using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.ProjectSystem;

namespace VSKubernetes
{
    [Export]
    [AppliesTo(Constants.uniqueCapability)]
    class KubernetesConfiguredProject
    {
        [Import, SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "MEF")]
        internal ConfiguredProject ConfiguredProject { get; private set; }

        [Import, SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "MEF")]
        internal ProjectProperties Properties { get; private set; }
    }
}
