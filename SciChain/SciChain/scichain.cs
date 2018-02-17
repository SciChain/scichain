using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using System;

/* Etapas:
        1 - Escritor envia um abstract para um editor
        2 - Editor aceita ou rejeita. Caso ele aceite, ele envia as public keys dos revisores para o escritor e o hash do processo.
        3 - O escritor envia o artigo criptografado em todas as publics keys e junto envia sua public key para o editor.
        4 - O editor envia os arquivos para cada revisor junto com a public key do escritor
        5 - Se ó artigo passar pelo critério ele é publicado com a assinatura do escritor, editor e revisores
        6 - Se o artigo for rejeitado, voltamos a etapa 3.


    Formato dos dados do processo
    bytes 0-5: status = { Rejeição, Confirmação, envio do artigo, aprovação, revisão, publicação } = new byte[] { 0, 0, 0, 0, 0, 0 }
    byte 6-fim: dado

    sempre o dado corrente. Segundo o status:
    Confirmação : no dado estará o abstract
    envio do artigo: no dado estará o artigo
    aprovação: no dado estará os comentários de cada revisor
    revisão: estará o artigo revisado
    publicação: estará vazio
    */

namespace SciChain
{
    public class SciChain : SmartContract
    {
        static byte[] editorPrefix = { 0, 0, 0 };
        static byte[] editorProcessPrefix = { 0, 0, 1 };
        static byte[] reviwersPrefix = { 0, 1, 1 };
        static byte[] processPrefix = { 1, 1, 0 };
        static byte[] publishPrefix = { 1, 1, 1 };
        static byte[] nonceProcessesPrefix = { 1, 0, 1 };

        public static Object Main( string operation, params object[] args )
        {
            switch ( operation )
            {
                case "SendToEditor":
                    if( args.Length != 3 )
                        return false;
                    return RequestArticle( (byte[])args[0], (byte[])args[1], (byte[])args[2] );
                case "SendToReviewers":
                    if( args.Length != 3 )
                        return false;
                    return SendDataToProcess( (byte[])args[0], (byte[])args[1], (byte[])args[2] );
                case "SendToPublisher":
                    if( args.Length != 2 )
                        return false;
                    return ReceiveFromProcess( (byte[])args[0], (byte[])args[1] );
                case "Publish":
                    if( args.Length != 2 )
                        return false;
                    return Publish( (byte[])args[0], (byte[])args[1] );
                case "RegisterEditor":
                    if( args.Length != 1 )
                        return false;
                    return RegisterEditor( (byte[])args[0] );
                case "RegisterReviewers":
                    if( args.Length != 2 )
                        return false;
                    return RegisterReviewer( (byte[])args[0], (byte[])args[1] );
                case "GetProcessStatus":
                    if( args.Length != 1 )
                        return false;
                    return GetProcessStatus( (byte[])args[0] );
                default:
                    return false;
            }
        }

        public static int GetProcessStatus( byte[] processId )
        {
            // status = { Rejeição, Confirmação, envio do artigo, aprovação, revisão, publicação } = new byte[] { 0, 0, 0, 0, 0, 0 }
            // resto dos bytes do processo são dados enviados.
            byte[] data = Storage.Get( Storage.CurrentContext, processId );

            if( data.Length == 0 )
            {
                Runtime.Notify( "Process not found" );
                return -1;
            }

            if ( data[0] == 1 )
            {
                Runtime.Notify( "Process Rejected" );
                return 1;
            }

            if (data[5] == 1)
            {
                Runtime.Notify( "Published" );
                return data.Length - 1;
            }

            for ( int i = 1; i < 6; ++i  )
            {
                if( data[i] == 0 )
                {
                    if( i == 1 )// Confirmação
                    {
                        Runtime.Notify( "Waiting Editor Acceptance" );
                    }
                    else if( i == 2 )// Envio do artigo e da pk do escritor
                    {
                        Runtime.Notify( "Waiting article" );
                    }
                    else if( i == 3 ) // Aprovação
                    {
                        Runtime.Notify( "Waiting aprroval" );
                    }
                    else if( i == 4 ) // Publicado
                    {
                        Runtime.Notify( "Waiting for publication" );
                    }
                    else // revisão
                    {
                        Runtime.Notify( "Waiting revision" );
                    }
                    return i;
                }
            }

            return -1;
        }

        public static byte[] RequestArticle( byte[] data, byte[] publisherAdress, byte[] editorAdress )
        {
            // Iniciando pedido de publicação
            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if ( Storage.Get(Storage.CurrentContext, editorKey) != editorAdress )
            {
                Runtime.Notify("Editor not found");
                return null;
            }

            byte[] nonceProcessesKey = nonceProcessesPrefix;
            nonceProcessesKey.Concat( editorKey );
            byte[] nonce = Storage.Get( Storage.CurrentContext, nonceProcessesKey );
            nonce.Concat( new byte[] { 1 } );
            Storage.Put( Storage.CurrentContext, nonceProcessesKey, nonce );

            byte[] processKey = processPrefix;
            processKey.Concat( nonce ); // unicidade do processo
            processKey.Concat( editorAdress );
            processKey.Concat( publisherAdress );

            Storage.Put( Storage.CurrentContext, processKey, new byte[] { 0, 0, 0, 0, 0, 0 } ); // criado apenas os status

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAdress );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );
            processes.Concat( processKey );
            Storage.Put( Storage.CurrentContext, epKey, processes );

            return processKey;
        }

        public static bool SendDataToProcess( byte[] ownAdress, byte[] data, byte[] process )
        {
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return true;
        }

        public static byte[] ReceiveFromProcess( byte[] ownAdress, byte[] process )
        {
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return null;
        }

        public static bool Publish( byte[] data, byte[] processId )
        {
            if (data.Length <= 0)
            {
                Runtime.Notify( "No data found" );
                return false;
            }

            byte[] publishKey = publishPrefix;
            publishKey.Concat( processId );

            if ( Storage.Get( Storage.CurrentContext, publishKey ).Length >= 0 )
            {
                Runtime.Notify("It was already published");
                return false;
            }

            Storage.Put( Storage.CurrentContext, publishKey, data );
            return true;
        }

        public static bool RegisterEditor( byte[] editorAdress )
        {
            byte[] editorKey = editorPrefix;
            editorKey.Concat(editorAdress);

            if ( Storage.Get( Storage.CurrentContext, editorKey) == editorAdress )
            {
                Runtime.Notify( "Editor is already registered" );
                return false;
            }

            Storage.Put( Storage.CurrentContext, editorKey, editorAdress );
            Runtime.Notify( "Editor registered" );
            return true;
        }

        public static bool RegisterReviewer( byte[] editorAdress, byte[] ReviewerAdress )
        {
            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if (Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress)
            {
                Runtime.Notify( "Editor not found" );
                return false;
            }

            byte[] reviewersKey = reviwersPrefix;
            editorKey.Concat( editorKey );

            byte[] reviewers = Storage.Get( Storage.CurrentContext, reviewersKey );
            reviewers.Concat( ReviewerAdress );

            Storage.Put( Storage.CurrentContext, reviewersKey, reviewers );
            Runtime.Notify( "Reviewer registered" );
            return true;
        }
    }
}
