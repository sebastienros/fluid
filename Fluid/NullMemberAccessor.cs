﻿namespace Fluid
{
    public class NullMemberAccessor : IMemberAccessor
    {
        public static readonly IMemberAccessor Instance = new NullMemberAccessor();

        private NullMemberAccessor()
        {

        }
        
        object IMemberAccessor.Get(object obj, string name, TemplateContext ctx)
        {
            return null;
        }
    }
}
