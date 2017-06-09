using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

namespace VSKubernetes
{
    [Export(typeof(IProjectTreePropertiesProvider))]
    [AppliesTo(Constants.uniqueCapability)]
    [Order(1000)]
    class ProjectTreeModifier : IProjectTreePropertiesProvider
    {
        [Import]
        public UnconfiguredProject UnconfiguredProject { get; set; }

        public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext propertyContext, IProjectTreeCustomizablePropertyValues propertyValues)
        {
            // Only set the icon for the root project node.  We could choose to set different icons for nodes based
            // on various criteria, not just Capabilities, if we wished.
            if (propertyValues.Flags.Contains(ProjectTreeFlags.Common.ProjectRoot))
            {
                // TODO: Provide a moniker that represents the desired icon (you can use the "Custom Icons" item template to add a .imagemanifest to the project)
                propertyValues.Icon = ImageMonikers.KubernetesProject.ToProjectSystemType();
            }
        }
    }
}