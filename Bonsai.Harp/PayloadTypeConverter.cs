﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Harp
{
    class PayloadTypeConverter : EnumConverter
    {
        public PayloadTypeConverter()
            : base(typeof(PayloadType))
        {
        }

        public override TypeConverter.StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            var fields = EnumType
                .GetFields().Where(field => field.IsStatic)
                .OrderBy(field => field.MetadataToken)
                .Select(field => field.GetValue(null))
                .ToArray();
            return new StandardValuesCollection(fields);
        }
    }
}
