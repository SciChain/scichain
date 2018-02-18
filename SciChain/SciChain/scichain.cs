using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System.Linq;

/* Etapas:
        1 - Escritor envia um abstract para um editor
        2 - Editor aceita ou rejeita. Caso ele aceite, ele envia as public keys dos revisores para o escritor e o hash do processo.
        3 - O escritor envia o artigo criptografado em todas as publics keys e junto envia sua public key para o editor.
        4 - O editor envia os arquivos para cada revisor junto com a public key do escritor
        5 - Se ó artigo passar pelo critério ele é publicado com a assinatura do escritor, editor e revisores
        6 - Se o artigo for rejeitado, voltamos a etapa 3.


    Lista de dados:
    0: status = { não existe, Rejeição, Confirmação, envio do artigo, aprovação, publicação } ( 1 byte )
    1: Endereço do escritor ( 32 bytes )
    2: Endereço do editor ( 32 bytes )
    3: número de revisores ( 1 byte )
    3: Endereço dos revisores ( 32 bytes por revisor )
    4-n: dados

    sempre o dado corrente. Segundo o status:
    Confirmação : no dado estará o abstract
    envio do artigo: no dado estará o artigo
    aprovação: no dado estará os comentários de cada revisor
    revisão: estará o artigo revisado
    publicação: estará o adress do escritor, do editor e dos revisores

    */

namespace SciChain
{
    public class SciChain : SmartContract
    {
        /* enum Process_Status 
         * { Not_Found, 0
         * Process_Rejected,  1
         * Waiting_Editor_Acceptance,  2
         * Waiting_article,  3
         * Waiting_approval,  4
         * Waiting_for_publication,  5
         * Published 6 };
         */

        static byte[] editorPrefix = { 0 };
        static byte[] editorProcessPrefix = { 1 };
        static byte[] reviewerPrefix = { 2 };
        static byte[] reviewersPrefix = { 3 };
        static byte[] processPrefix = { 4 };
        static byte[] publishPrefix = { 5 };

        public static object Main( string operation, params object[] args )
        {
            switch (operation)
            {
                case "GetProcessStatus":
                    if( args.Length != 1 ) return false;
                    return GetProcessStatus( (byte[])args[0] );
                case "RequestArticle":
                    if( args.Length != 2 ) return false;
                    return RequestArticle( (byte[])args[0], (byte[])args[1] );
                case "SendDataToProcess":
                    if( args.Length != 2 ) return false;
                    return SendDataToProcess( (byte[])args[0], (byte[])args[1] );
                case "ReceiveFromProcess":
                    if( args.Length != 1 ) return false;
                    return ReceiveFromProcess( (byte[])args[0] );
                case "Publish":
                    if( args.Length != 2 ) return false;
                    return Publish( (byte[])args[0], (byte[])args[1] );
                case "RegisterEditor":
                    if( args.Length != 0 ) return false;
                    return RegisterEditor();
                case "RegisterReviewer":
                    if( args.Length != 1 ) return false;
                    return RegisterReviewer( (byte[])args[0] );
                default:
                    return false;
            }
        }

        public static byte GetProcessStatus( byte[] processId )
        {
            return Storage.Get( Storage.CurrentContext, processId )[0];
        }

        public static byte[] RequestArticle( byte[] data, byte[] editorAdress )
        {
            // Iniciando pedido de publicação

            byte[] publisherAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );
            editorKey = Hash256( editorKey );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify( "Editor not found" );
                return null;
            }

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAdress );
            epKey = Hash256( epKey );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );

            byte[] processKey = processPrefix;
            processKey.Concat(Hash256( processes ) ); // unicidade do processo
            processKey.Concat( editorAdress );
            processKey.Concat( publisherAdress );
            processKey = Hash256( processKey );

            Storage.Put( Storage.CurrentContext, processKey, new byte[] { 2 } ); // criado apenas os status

            processes.Concat( processKey );
            Storage.Put( Storage.CurrentContext, epKey, processes );

            return processKey;
        }

        public static bool SendDataToProcess( byte[] process, byte[] data )
        {
            byte[] ownAdress = ExecutionEngine.CallingScriptHash;
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return true;
        }

        public static byte[] ReceiveFromProcess( byte[] process )
        {
            byte[] ownAdress = ExecutionEngine.CallingScriptHash;
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return null;
        }

        public static bool Publish( byte[] data, byte[] processId )
        {
            if( data.Length <= 0 )
            {
                Runtime.Notify( "No data found" );
                return false;
            }

            if( GetProcessStatus( processId ) != 5 )
            {
                Runtime.Notify( "Can't publish" );
                return false;
            }

            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );
            editorKey = Hash256( editorKey );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAdress );
            epKey = Hash256( epKey );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );

            for( int i = 0; i < processes.Length; i += 256 )
            {
                if( processes.Range( i, 256 ) == processId)
                {
                    byte[] publishKey = publishPrefix;
                    publishKey.Concat( processId );
                    publishKey = Hash256( publishKey );

                    if( Storage.Get( Storage.CurrentContext, publishKey ).Length >= 0 )
                    {
                        Runtime.Notify( "It was already published" );
                        return false;
                    }

                    data.Concat( processId );

                    Storage.Put( Storage.CurrentContext, publishKey, data );
                    return true;
                }
            }

            Runtime.Notify( "Not a process of this Editor" );
            return false;
        }

        public static byte[] RegisterEditor()
        {
            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );
            editorKey = Hash256( editorKey );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) == editorAdress )
            {
                Runtime.Notify( "Editor is already registered" );
                return null;
            }

            Storage.Put( Storage.CurrentContext, editorKey, editorAdress );
            Runtime.Notify( "Editor registered" );

            return editorKey;
        }

        public static bool RegisterReviewer( byte[] ReviewerAdress )
        {
            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );
            editorKey = Hash256( editorKey );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] reviewersKey = reviewersPrefix;
            reviewersKey.Concat( editorKey );
            reviewersKey = Hash256( reviewersKey );

            byte[] reviewers = Storage.Get( Storage.CurrentContext, reviewersKey );

            for( int i = 0; i < reviewers.Length; i += 256 )
            {
                if( reviewers.Range( i, 256 ) == ReviewerAdress )
                {
                    Runtime.Notify( "Reviwer already registered" );
                    return false;
                }  
            }

            reviewers.Concat( ReviewerAdress );

            Storage.Put( Storage.CurrentContext, reviewersKey, reviewers );
            Runtime.Notify( "Reviewer registered" );
            return true;
        }
    }
}
