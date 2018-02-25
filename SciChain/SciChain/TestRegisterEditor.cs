using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Linq;

namespace Neo.SmartContract
{
    public class HelloWorld : Framework.SmartContract
    {
        private static byte[] editorPrefix = { 1 };

        public static object Main( string operation, params object[] args )
        {
            if( operation == "RegisterEditor()" )
            {
                if( args.Length != 1 ) return false;
                return RegisterEditor( (byte[])args[0] );
            }

            return false;
        }

        public static byte[] RegisterEditor( byte[] address )
        {
            byte[] editorAddress = address;

            if( !Runtime.CheckWitness( editorAddress ) )
                return null;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAddress );
            editorKey = Hash256( editorKey );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) == editorAddress )
            {
                Runtime.Notify( "Editor is already registered" );
                return editorKey;
            }

            Storage.Put( Storage.CurrentContext, editorKey, editorAddress );
            Runtime.Notify( "Editor registered" );

            return editorKey;
        }
    }
}
