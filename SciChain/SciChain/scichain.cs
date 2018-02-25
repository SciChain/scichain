using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Linq;

/* Etapas:
        1 - Escritor envia um abstract para um editor
        2 - Editor aceita ou rejeita. Caso ele aceite, ele envia as public keys dos revisores para o escritor e o hash do processo.
        3 - O escritor envia o artigo criptografado em todas as publics keys e junto envia sua public key para o editor.
        4 - O editor envia os arquivos para cada revisor junto com a public key do escritor
        5 - Se ó artigo passar pelo critério ele é publicado com a assinatura do escritor, editor e revisores
        6 - Se o artigo for rejeitado, voltamos a etapa 3.


    Lista de dados:
    0: status ( 1 byte ) :
         * Not_Found 0
         * Process_Rejected  1
         * Waiting_Editor_Acceptance  2
         * Waiting_article  3
         * Waiting_rating 4
         * Waiting_approval,  5
         * Waiting_decrypto_article 6
         * waiting_reviewers_approval 7
         * Waiting_for_publication  8
         * Published 9
      
    1: key do autor/autores ( 32 bytes )
    2: key do editor ( 32 bytes )
    3: número de revisores ( 1 byte )
    3: key dos revisores ( 32 bytes por revisor )
    4-n: dados

    sempre o dado corrente. Segundo o status:
    Confirmação : no dado estará o abstract
    envio do artigo: no dado estará o artigo
    aprovação: no dado estará os comentários de cada revisor
    revisão: estará o artigo revisado
    publicação: estará o adress do escritor, do editor e dos revisores

    */

namespace Neo.SmartContract
{
    public class SciChain : Framework.SmartContract
    {
        private static byte[] autorPrefix = { 0 };
        private static byte[] editorPrefix = { 1 };
        private static byte[] editorProcessPrefix = { 2 };
        private static byte[] reviewerPrefix = { 3 };
        private static byte[] reviewersPrefix = { 4 };
        private static byte[] processPrefix = { 5 };
        private static byte[] publishPrefix = { 6 };
        private static byte[] reviewerCommentsPrefix = { 7 };

        public static object Main( string operation, params object[] args )
        {

            if( operation == "GetProcessStatus()" )
            {
                if( args.Length != 1 ) return false;
                return GetProcessStatus( (byte[])args[0] );
            }

            if( operation == "RequestArticle()" )
            {
                if( args.Length != 3 ) return false;
                return RequestArticle( (byte[])args[0], (byte[])args[1], (byte[])args[2] );
            }

            if( operation == "SendDataToProcess()" )
            {
                if( args.Length != 3 ) return false;
                return SendDataToProcess( (byte[])args[0], (byte[])args[1], (byte[])args[2] );
            }

            if( operation == "ReceiveFromProcess()" )
            {
                if( args.Length != 2 ) return false;
                return ReceiveFromProcess( (byte[])args[0], (byte[])args[1] );
            }

            if( operation == "Publish()" )
            {
                if( args.Length != 2 ) return false;
                return Publish( (byte[])args[0], (byte[])args[1] );
            }

            if( operation == "RegisterEditor()" )
            {
                if( args.Length != 1 ) return false;
                return RegisterEditor( (byte[])args[0] );
            }

            if( operation == "RegisterReviewer()" )
            {
                if( args.Length != 2 ) return false;
                return RegisterReviewer( (byte[])args[0], (byte[])args[1] );
            }

            return false;
        }

        public static byte GetProcessStatus( byte[] processkey )
        {
            return Storage.Get( Storage.CurrentContext, processkey )[0];
        }

        public static byte[] RequestArticle( byte[] address, byte[] data, byte[] editorAddress )
        {
            byte[] autorAddress = address;

            if( !VerifyWitness( autorAddress ) )
                return null;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( autorAddress );
            editorKey = Hash256( editorKey );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != autorAddress )
            {
                Runtime.Notify( "Editor not found" );
                return null;
            }

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAddress );
            epKey = Hash256( epKey );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );

            byte[] processKey = processPrefix;
            processKey.Concat( Hash256( processes ) );
            processKey.Concat( editorAddress );
            processKey.Concat( autorAddress );
            processKey = Hash256( processKey );

            processes.Concat( processKey );
            Storage.Put( Storage.CurrentContext, epKey, processes );

            byte[] autorKey = autorPrefix;
            autorKey.Concat( processKey );
            autorKey.Concat( editorAddress );
            autorKey = Hash256( autorKey );

            byte[] processData = new byte[] { 2 }; //status
            processData.Concat( autorKey );
            processData.Concat( editorKey );
            processData.Concat( new byte[] { 0 } ); // número de revisores
            processData.Concat( data ); // abstract

            Storage.Put( Storage.CurrentContext, processKey, processData );

            return processKey;
        }

        public static bool SendDataToProcess( byte[] address, byte[] processkey, byte[] data )
        {
            if ( data.Length == 0 )
            {
                Runtime.Notify( "Empty data" );
                return false;
            }

            byte[] ownAddress = address;

            if( !VerifyWitness( ownAddress ) )
                return false;

            byte[] processData = Storage.Get( Storage.CurrentContext, processkey );
            byte status = processData[0];

            if( status == 0 )
            {
                Runtime.Notify( "Can't send data to a undocumented process key" );
                return false;
            }

            if( status == 1 )
            {
                Runtime.Notify( "Can't send data to a rejected process key" );
                return false;
            }

            if( status == 8 )
            {
                Runtime.Notify( "Can't send data to a process key that's waiting to be published" );
                return false;
            }

            if ( status == 9 )
            {
                Runtime.Notify( "Can't send data to a published process key" );
                return false;
            }

            if ( status == 2 )
            {
                byte[] editorKey = editorPrefix;
                editorKey.Concat( ownAddress );
                editorKey = Hash256( editorKey );

                if( processData.Range( 33, 32 ) == editorKey )
                {
                    Runtime.Notify( "Not the article editor" );
                    return false;
                }

                if ( data[0] != 1 || data[0] != 3 )
                {
                    Runtime.Notify( "A status data must be Rejected(1) or Waiting article(3)" );
                    return false;
                }

                processData = processData.Range( 0, 65 );

                
                processData[0] = data[0];
                processData.Concat( data.Range( 1, data.Length - 1 ) ); // colocando os dados dos revisores ( número de revisores ( 1 byte ) + conjunto de 32 bytes a key de cada editor
                Storage.Put( Storage.CurrentContext, processkey, processData );

                return true;
            }

            if( status == 3 )
            {
                byte[] autorKey = autorPrefix;
                autorKey.Concat( processkey );
                autorKey.Concat( ownAddress );
                autorKey = Hash256( autorKey );

                if( processData.Range( 1, 32 ) == autorKey )
                {
                    Runtime.Notify( "Not the article autor" );
                    return false;
                }

                processData[0] = 4;
                processData.Concat( new byte[] { 0 } ); // um byte contador de avaliações
                processData.Concat( data ); // enviando a chave simétrica criptografada com a chave publica de cada revisor + artigo criptografado com a chave simétrica
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if( status == 4 )
            {
                byte[] reviewerKey = reviewerPrefix;
                reviewerKey.Concat( processkey );
                reviewerKey.Concat( ownAddress );
                reviewerKey = Hash256( reviewerKey );

                int idx = ( 66 + 32 * processData[65] );
                for ( int i = 66; i < ( 66 + 32*processData[65] ); i += 32 )
                {
                    if( processData.Range( i, 32 ) == reviewerKey )
                    {
                        byte[] reviewerCommentsKey = reviewerCommentsPrefix;
                        reviewerCommentsKey.Concat( processkey );
                        reviewerCommentsKey.Concat( reviewerKey );
                        reviewerCommentsKey = Hash256( reviewerCommentsKey );

                        byte[] reviewerComments = Storage.Get( Storage.CurrentContext, reviewerCommentsKey );

                        if( reviewerCommentsKey.Length > 0 )
                        {
                            Runtime.Notify( "Already rated" );
                            return false;
                        }

                        Storage.Put( Storage.CurrentContext, reviewerCommentsKey, data );

                        processData[idx] = (byte)(processData[idx] + 1);
                        if (processData[idx] == processData[65])
                            processData[0] = 5;

                        processData.Concat( reviewerCommentsKey );
                        Storage.Put( Storage.CurrentContext, processkey, processData );
                        return true;
                    }
                }

                Runtime.Notify( "Not the article reviewer" );
                return false;
            }

            if( status == 5 )
            {
                byte[] editorKey = editorPrefix;
                editorKey.Concat( ownAddress );
                editorKey = Hash256( editorKey );

                if( processData.Range( 33, 32 ) == editorKey )
                {
                    Runtime.Notify( "Not the article editor" );
                    return false;
                }

                if ( data[0] != 1 || data[0] != 6 )
                {
                    Runtime.Notify( "A status data must be Rejected(1) or Waiting publishing(6)" );
                    return false;
                }

                processData[0] = data[0];
                processData = processData.Range( 0, ( 66 + 32 * processData[65] ) ); // limpando dados anteriores

                int len = processData[65];
                byte[] numApproval = new byte[] { };
                for( int i = 0; i < len; ++i )
                    numApproval.Concat( new byte[] { 0 } );
                
                processData.Concat( numApproval ); // adicionando campos para os revisores avaliarem se o artigo que o autor colocará sem criptografia foi o mesmo que eles avaliaram.
                                                   // 1 não aprovado e 2 aprovado
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if( status == 6 )
            {
                byte[] autorKey = autorPrefix;
                autorKey.Concat( processkey );
                autorKey.Concat( ownAddress );
                autorKey = Hash256( autorKey );

                if( processData.Range( 1, 32 ) == autorKey )
                {
                    Runtime.Notify( "Not the article autor" );
                    return false;
                }

                processData[0] = 7;
                processData.Concat( data ); // artigo descriptografado
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if ( status == 7 )
            {
                if( data[0] != 1 || data[0] != 2 )
                {
                    Runtime.Notify( "Data must be Rejected(1) or Aprroved(2)" );
                    return false;
                }

                byte[] reviewerKey = reviewerPrefix;
                reviewerKey.Concat( processkey );
                reviewerKey.Concat( ownAddress );
                reviewerKey = Hash256( reviewerKey );

                int numA = 0;
                for( int i = 66, count = 0; i < (66 + 32 * processData[65]); i += 32, count++ )
                {
                    int idx = ( 66 + 32 * processData[65] ) + count;
                    if( processData[idx] != 0 )
                        numA++;
                    if ( processData.Range( i, 32 ) == reviewerKey )
                    {
                        processData[idx] = data[0];
                    }
                }

                if( numA == processData[65] )// todos avaliaram
                {
                    processData[0] = 8;
                    Storage.Put( Storage.CurrentContext, processkey, processData );
                }
                return true;
            }

            Runtime.Notify( "Not the article reviewer" );
            return false;
        }

        public static byte[] ReceiveFromProcess( byte[] address, byte[] processkey )
        {
            byte[] ownAddress = address;

            if( !VerifyWitness( ownAddress ) )
                return null;

            byte[] processData = Storage.Get( Storage.CurrentContext, processkey );

            byte[] autorKey = autorPrefix;
            autorKey.Concat( processkey );
            autorKey.Concat( ownAddress );
            autorKey = Hash256( autorKey );

            if( processData.Range( 1, 32 ) != autorKey )
            {
                byte[] editorKey = editorPrefix;
                editorKey.Concat( ownAddress );
                editorKey = Hash256( editorKey );

                if( processData.Range( 33, 32 ) != editorKey )
                {
                    byte[] reviewerKey = reviewerPrefix;
                    reviewerKey.Concat( processkey );
                    reviewerKey.Concat( ownAddress );
                    reviewerKey = Hash256( reviewerKey );

                    bool ok = false;
                    for( int i = 66; i < ( 66 + 32 * processData[65] ); i += 32 )
                    {
                        if ( processData.Range( i, 32 ) == reviewerKey )
                        {
                            ok = true;
                        }
                    }

                    if( !ok )
                    {
                        Runtime.Notify( "Access denied" );
                        return null;
                    }
                }   
            }

            return processData;
        }

        public static bool Publish( byte[] address, byte[] processkey )
        {
            if( GetProcessStatus( processkey ) != 8 )
            {
                Runtime.Notify( "Can't publish" );
                return false;
            }

            byte[] editorAddress = address;

            if( !VerifyWitness( editorAddress ) )
                return false;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAddress );
            editorKey = Hash256( editorKey );

            if( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] epKey = editorProcessPrefix;
            epKey.Concat( editorAddress );
            epKey = Hash256( epKey );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );

            for( int i = 0; i < processes.Length; i += 32 )
            {
                if( processes.Range( i, 32 ) == processkey )
                {
                    byte[] publishKey = publishPrefix;
                    publishKey.Concat( processkey );
                    publishKey = Hash256( publishKey );

                    if( Storage.Get( Storage.CurrentContext, publishKey ).Length >= 0 )
                    {
                        Runtime.Notify( "It was already published" );
                        return false;
                    }

                    Runtime.Notify( "Published" );
                    return true;
                }
            }

            Runtime.Notify( "Not a process of this Editor" );
            return false;
        }

        public static byte[] RegisterEditor( byte[] address )
        {
            byte[] editorAddress = address;

            if( !VerifyWitness( editorAddress ) )
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

        public static bool RegisterReviewer( byte[] address, byte[] ReviewerAddress )
        {
            byte[] editorAddress = address;

            if( !VerifyWitness( editorAddress ) )
                return false;

            byte[] editorKey = editorPrefix;
            editorKey.Concat( editorAddress );
            editorKey = Hash256( editorKey );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] reviewersKey = reviewersPrefix;
            reviewersKey.Concat( editorKey );
            reviewersKey = Hash256( reviewersKey );

            byte[] reviewers = Storage.Get( Storage.CurrentContext, reviewersKey );

            for( int i = 0; i < reviewers.Length; i += 32 )
            {
                if( reviewers.Range( i, 32 ) == ReviewerAddress )
                {
                    Runtime.Notify( "Reviwer already registered" );
                    return false;
                }  
            }

            reviewers.Concat( ReviewerAddress );

            Storage.Put( Storage.CurrentContext, reviewersKey, reviewers );
            Runtime.Notify( "Reviewer registered" );
            return true;
        }

        private static bool VerifyWitness( byte[] address )
        {
            bool ok = Runtime.CheckWitness( address );
            if( !ok )
                Runtime.Notify( "You are not the address" );
            return ok;
        }
    }
}
