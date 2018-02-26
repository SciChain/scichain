using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;
using System.Numerics;

namespace Neo.SmartContract
{
    public class SmartRules : Framework.SmartContract
    {
        /* 
         This is a smart rule. You can set the editor approval automatically
         This is a default template, you can create yours if you want to
         This function will return the number -1 if it was not possible to calculate
         number 0 if the article passed
         number 1 if the article was rejected
         the int[] grades has only grades beteween 0 and 10 from all the reviewers
         */
        public static int Main( int[] grades, string opt, int MintoApproval )
        {
            if( grades.Length < 3 )
            {
                Runtime.Notify( "Can't calculate with less then 3 grades" );
                return -1;
            }


            if( opt == "Average()" )
            {
                if( Average( grades ) >= MintoApproval ) return 0; else return 1;
            }

            if (opt == "PopBigger()")
            {
                int[] u = PopBigger( grades );
                if( Average( u ) >= MintoApproval ) return 0; else return 1;
            }

            if (opt == "PopSmaller()")
            {
                int[] u = PopSmaller( grades );
                if( Average( u ) >= MintoApproval ) return 0; else return 1;
            }

            if (opt == "PopBiggerSmaller()")
            {
                int[] u = PopBigger( PopSmaller( grades ) );
                if( Average( u ) >= MintoApproval ) return 0; else return 1;
            }

            return -1;
        }

        private static int Average( int[] grades )
        {
            int sum = 0;
            for( int i = 0; i < grades.Length; ++i )
                sum += grades[i];
            return sum/grades.Length;
        }

        private static int[] PopBigger( int[] grades )
        {
            int bigger = -1;
            int[] u = new int[grades.Length - 1];
            int idx = 0;
            for (int i = 0; i < grades.Length; ++i)
            {
                if( grades[i] > bigger )
                {
                    if( bigger != -1 )
                    {
                        u[idx] = bigger;
                        ++idx;
                    }
                    bigger = grades[i];
                }
            }
            return u;
        }

        private static int[] PopSmaller( int[] grades )
        {
            int smaller = 99;
            int[] u = new int[grades.Length - 1];
            int idx = 0;
            for( int i = 0; i < grades.Length; ++i )
            {
                if( grades[i] < smaller )
                {
                    if( smaller != 99 )
                    {
                        u[idx] = smaller;
                        ++idx;
                    }
                    smaller = grades[i];
                }
            }
            return u;
        }
    }
}
