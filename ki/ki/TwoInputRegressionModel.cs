﻿using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ki
{
    class TwoInputRegressionModel
    {
        [LoadColumn(2)]
        public float Population;
        [LoadColumn(4)]
        public float Year;

        [LoadColumn(6)]
        public float Co2;
    }
    public class TwoInputRegressionPrediction
    {
        [ColumnName("Score")]
        public float Co2;
    }
}