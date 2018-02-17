using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Collections.Generic;
using System.Linq;

/* Etapas:
        1 - Escritor envia um abstract para um editor
        2 - Editor aceita ou rejeita. Caso ele aceite, ele envia as public keys dos revisores para o escritor e o hash do processo.
        3 - O escritor envia o artigo criptografado em todas as publics keys e junto envia sua public key para o editor.
        4 - O editor envia os arquivos para cada revisor junto com a public key do escritor
        5 - Se ó artigo passar pelo critério ele é publicado com a assinatura do escritor, editor e revisores
        6 - Se o artigo for rejeitado, voltamos a etapa 3.


    Lista de dados:
    0: status = { Rejeição, Confirmação, envio do artigo, aprovação, publicação } = new byte[] { 0, 0, 0, 0, 0 }
    1: Endereço do escritor
    2: Endereço do editor
    3: Endereço dos revisores
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
        enum Process_Status { Not_Found, Process_Rejected, Waiting_Editor_Acceptance, Waiting_article, Waiting_approval, Waiting_for_publication, Published };
        static byte[] editorPrefix = { 0, 0, 0 };
        static byte[] editorProcessPrefix = { 0, 0, 1 };
        static byte[] reviewersPrefix = { 0, 1, 1 };
        static byte[] processPrefix = { 1, 1, 0 };
        static byte[] publishPrefix = { 1, 1, 1 };

        public static void Main()
        {
        }

        public int GetProcessStatus( byte[] processId )
        {
            List<String> process = Storage.Get( Storage.CurrentContext, processId ).ToString().Split( ';' ).OfType<String>().ToList();

            if( process.Count == 0 )
            {
                Runtime.Notify( "Process not found" );
                return (int)Process_Status.Not_Found;
            }

            return Convert.ToInt32( process[0] );
        }

        public byte[] RequestArticle( byte[] data, byte[] editorAdress )
        {
            // Iniciando pedido de publicação

            byte[] publisherAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify( "Editor not found" );
                return null;
            }

            // verificando número de processos do editor
            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAdress );

            List<String> processes = Storage.Get( Storage.CurrentContext, epKey ).ToString().Split(';').OfType<String>().ToList();

            byte[] processKey = processPrefix;
            processKey.Concat( processKey.Length.ToString().AsByteArray() ); // unicidade do processo
            processKey.Concat( editorAdress );
            processKey.Concat( publisherAdress );

            List<String> currProcess = new List<String> () { new byte[] { 0, 0, 0, 0, 0, 0 }.ToString() };
            Storage.Put( Storage.CurrentContext, processKey, String.Join( ";", currProcess ).AsByteArray() ); // criado apenas os status

            processes.Add( processKey.ToString() );
            Storage.Put( Storage.CurrentContext, epKey, String.Join( ";", processes ).AsByteArray() );

            return processKey;
        }

        public bool SendDataToProcess( byte[] process, byte[] data )
        {
            byte[] ownAdress = ExecutionEngine.CallingScriptHash;
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return true;
        }

        public byte[] ReceiveFromProcess( byte[] process )
        {
            byte[] ownAdress = ExecutionEngine.CallingScriptHash;
            // dependendo da parte do processo, essa função vai interpretar de uma forma
            // Os revisores e o escritor podem enviar dados pra o editor
            // apenas o editor pode enviar dados para os revisores
            // apenas o editor pode enviar dados para o escritor
            return null;
        }

        public bool Publish( byte[] data, byte[] processId )
        {
            if( data.Length <= 0 )
            {
                Runtime.Notify( "No data found" );
                return false;
            }

            if( GetProcessStatus( processId ) != (int)Process_Status.Waiting_for_publication )
            {
                Runtime.Notify( "Can't publish" );
                return false;
            }

            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAdress );

            List<String> processes = Storage.Get( Storage.CurrentContext, epKey ).ToString().Split(';').OfType<String>().ToList();

            if( !processes.Contains( processId.ToString() ) )
            {
                Runtime.Notify( "Not a process of this Editor" );
                return false;
            }

            byte[] publishKey = publishPrefix;
            publishKey.Concat( processId );

            if ( Storage.Get( Storage.CurrentContext, publishKey ).Length >= 0 )
            {
                Runtime.Notify("It was already published");
                return false;
            }

            data.Concat( processId );

            Storage.Put( Storage.CurrentContext, publishKey, data );
            return true;
        }

        public byte[] RegisterEditor()
        {
            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) == editorAdress )
            {
                Runtime.Notify( "Editor is already registered" );
                return null;
            }

            Storage.Put( Storage.CurrentContext, editorKey, editorAdress );
            Runtime.Notify( "Editor registered" );
            return editorAdress;
        }

        public bool RegisterReviewer( byte[] ReviewerAdress )
        {
            byte[] editorAdress = ExecutionEngine.CallingScriptHash;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAdress );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAdress )
            {
                Runtime.Notify("Not an Editor");
                return false;
            }

            byte[] reviewersKey = reviewersPrefix;
            editorKey.Concat( editorKey );

            List<String> reviewers = Storage.Get(Storage.CurrentContext, reviewersKey).ToString().Split(';').OfType<String>().ToList();

            if( reviewers.Contains( ReviewerAdress.ToString() ) )
            {
                Runtime.Notify( "Reviwer already registered" );
                return false;
            }

            reviewers.Add( ReviewerAdress.ToString() );

            Storage.Put( Storage.CurrentContext, reviewersKey, String.Join( ";", reviewers ).AsByteArray() );
            Runtime.Notify( "Reviewer registered" );
            return true;
        }
    }
}
