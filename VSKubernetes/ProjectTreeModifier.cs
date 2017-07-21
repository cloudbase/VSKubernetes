using System.ComponentModel.Composition;

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
            if (propertyValues.Flags.Contains(ProjectTreeFlags.Common.ProjectRoot))
            {
                propertyValues.Icon = ImageMonikers.KubernetesProject.ToProjectSystemType();
            }
        }
    }
}