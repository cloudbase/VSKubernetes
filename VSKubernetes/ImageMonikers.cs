using System;
using Microsoft.VisualStudio.Imaging.Interop;

namespace VSKubernetes
{
    public static class ImageMonikers
    {
        private static readonly Guid ManifestGuid = new Guid("c558abb7-ab5b-4430-ad5b-8218debf95f5");
        private const int KubernetesProjectIcon = 0;

        public static ImageMoniker KubernetesProject
        {
            get
            {
                return new ImageMoniker { Guid = ManifestGuid, Id = KubernetesProjectIcon };
            }
        }
    }
}