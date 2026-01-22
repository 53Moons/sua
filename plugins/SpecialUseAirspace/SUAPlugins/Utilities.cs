using Microsoft.Xrm.Sdk;
using System;

namespace SUAPlugins
{
    public static class Utilities
    {
        public static T GetValueOnUpdate<T>(Entity target, Entity preImage, string attribute)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (preImage == null)
                throw new ArgumentNullException("preImage");

            if (!target.TryGetAttributeValue(attribute, out T value))
            {
                preImage.TryGetAttributeValue(attribute, out value);
            }
            return value;
        }
    }
}
