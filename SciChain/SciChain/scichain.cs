using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Linq;

/*
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

    1: key do author/authores ( 32 bytes )
    2: key do editor ( 32 bytes )
    3: número de revisores ( 1 byte )
    3: key dos revisores ( 32 bytes por revisor )
    4-n: dados

    Endorse:
             * 0: hash do lvl -> local onde está o lvl do usuário ( 32 bytes )
             * 1: hash das skills e contadores ( 32 bytes ) -> contém todas as skills do usuário
             * 2: hash do contador de lvl ( 32 bytes )
             * os contadores são definidos pelo tamanho do array
    */

namespace Neo.SmartContract
{
    public class SciChain : Framework.SmartContract
    {
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

            if( operation == "Endorse()")
            {
                if( args.Length != 3 ) return false;
                return Endorse( (byte[])args[0], (byte[])args[1], (byte[])args[2] );
            }

            if( operation == "GetEndorseData()")
            {
                if( args.Length != 1 ) return false;
                return GetEndorseData( (byte[])args[0] );
            }

            return false;
        }

        public static byte GetProcessStatus( byte[] processkey )
        {
            byte[] processData = Storage.Get( Storage.CurrentContext, processkey );
            Runtime.Notify("restoring processKey => processData: ");
            Runtime.Notify(processData);
            
            return processData[0];
        }

        /*First steps of a manuscript submission
         recieves WIF address of main author, abstract and WIF of the editor that will handle it
         verifies if Editor is registered and returns the processKey with abstract and both publicKeys
        */ 
        public static byte[] RequestArticle( byte[] address, byte[] data, byte[] editorAddress )
        {
            byte[] authorAddress = address;

            if( !VerifyWitness( authorAddress ) )
                return null;

            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
            editorKey = Hash256(editorKey);

            Runtime.Notify("using editorKey:");
            Runtime.Notify(editorKey);

            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Editor not found" );
                return null;
            }

            byte[] epKey = editorAddress.Concat("editorProcess".AsByteArray());
            epKey = Hash256(epKey);

            Runtime.Notify("using epKey:");
            Runtime.Notify(epKey);

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );
            Runtime.Notify("processes:");
            Runtime.Notify(processes);

            byte[] processKey = Hash256(processes).Concat("Process".AsByteArray());
            processKey = processKey.Concat(editorAddress);
            processKey = processKey.Concat(authorAddress);
            processKey = Hash256(processKey);

            Runtime.Notify("middle processKey:");
            Runtime.Notify(processKey);

            processes = processes.Concat( processKey );
            Storage.Put( Storage.CurrentContext, epKey, processes );

            Runtime.Notify("epKey => processes: ");
            Runtime.Notify(processes);

            byte[] authorKey = processKey.Concat("Author".AsByteArray());
            authorKey = authorKey.Concat( editorAddress );
            authorKey = Hash256( authorKey );

            Runtime.Notify("authorKey:");
            Runtime.Notify(authorKey);

            byte[] processData = new byte[] { 2 }; //status
            processData = processData.Concat( authorKey );
            processData = processData.Concat( editorKey );
            processData = processData.Concat( new byte[] { 0 } ); // number of reviewers
            processData = processData.Concat( data ); // abstract

            Runtime.Notify("processKey => processData: ");
            Runtime.Notify(processData);

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

            Runtime.Notify("restaured processKey => processData: ");
            Runtime.Notify(processData);
            Runtime.Notify("status is:");
            Runtime.Notify(status);

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
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if ( processData.Range( 33, 32 ) != editorKey )
                {
                    Runtime.Notify( "Not the article editor" );
                    return false;
                }

                if ( data[0] == 1 || data[0] == 3 )
                {
                    processData = processData.Range(0, 65);
                    Runtime.Notify("data:");
                    Runtime.Notify(data);

                    processData[0] = data[0];
                    processData = processData.Concat(data.Range(1, data.Length - 1)); // colocando os dados dos revisores ( número de revisores ( 1 byte ) + conjunto de 32 bytes a key de cada editor
                    Storage.Put(Storage.CurrentContext, processkey, processData);

                    return true;
                }

                Runtime.Notify("A status data must be Rejected(1) or Waiting article(3)");
                return false;
            }

            if( status == 3 )
            {
                byte[] authorKey = processkey.Concat("Author".AsByteArray());
                authorKey.Concat( ownAddress );
                authorKey = Hash256( authorKey );

                if( processData.Range( 1, 32 ) != authorKey )
                {
                    Runtime.Notify( "Not the article author" );
                    return false;
                }

                processData[0] = 4;
                processData = processData.Concat( new byte[] { 0 } ); // um byte contador de avaliações
                processData = processData.Concat( data ); // enviando a chave simétrica criptografada com a chave publica de cada revisor + artigo criptografado com a chave simétrica
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if( status == 4 )
            {
                byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                reviewerKey = reviewerKey.Concat( ownAddress );
                reviewerKey = Hash256( reviewerKey );

                int idx = ( 66 + 32 * processData[65] );
                for ( int i = 66; i < ( 66 + 32*processData[65] ); i += 32 )
                {
                    if( processData.Range( i, 32 ) == reviewerKey )
                    {
                        byte[] reviewerCommentsKey = processkey.Concat("ReviewerComments".AsByteArray());
                        reviewerCommentsKey = reviewerCommentsKey.Concat( reviewerKey );
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
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if ( processData.Range( 33, 32 ) != editorKey )
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

                processData = processData.Concat( numApproval ); // adicionando campos para os revisores avaliarem se o artigo que o author colocará sem criptografia foi o mesmo que eles avaliaram.
                                                   // 1 não aprovado e 2 aprovado
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if( status == 6 )
            {
                byte[] authorKey = processkey.Concat("Author".AsByteArray());
                authorKey = authorKey.Concat( ownAddress );
                authorKey = Hash256( authorKey );

                if( processData.Range( 1, 32 ) != authorKey )
                {
                    Runtime.Notify( "Not the article author" );
                    return false;
                }

                processData[0] = 7;
                processData = processData.Concat( data ); // artigo descriptografado
                Storage.Put( Storage.CurrentContext, processkey, processData );
                return true;
            }

            if ( status == 7 )
            {
                if( data[0] == 1 || data[0] == 2 )
                {
                    byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                    reviewerKey = reviewerKey.Concat(ownAddress);
                    reviewerKey = Hash256(reviewerKey);

                    int numA = 0;
                    for (int i = 66, count = 0; i < (66 + 32 * processData[65]); i += 32, count++)
                    {
                        int idx = (66 + 32 * processData[65]) + count;
                        if (processData[idx] != 0)
                            numA++;
                        if (processData.Range(i, 32) == reviewerKey)
                        {
                            processData[idx] = data[0];
                        }
                    }

                    if (numA == processData[65])// todos avaliaram
                    {
                        processData[0] = 8;
                        Storage.Put(Storage.CurrentContext, processkey, processData);
                    }
                    return true;
                }

                Runtime.Notify("Data must be Rejected(1) or Aprroved(2)");
                return false;
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
            Runtime.Notify("restoring processKey => processData: ");
            Runtime.Notify(processData);
            
            byte[] authorKey = processkey.Concat("Author".AsByteArray());
            authorKey = authorKey.Concat( ownAddress );
            authorKey = Hash256( authorKey );
            Runtime.Notify("authorKey: ");
            Runtime.Notify(authorKey);


            if( processData.Range( 1, 32 ) != authorKey )
            {
                Runtime.Notify( "Right author key" );
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if ( processData.Range( 33, 32 ) != editorKey )
                {
                    byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                    reviewerKey = reviewerKey.Concat( ownAddress );
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

            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
            editorKey = Hash256(editorKey);

            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] epKey = editorAddress.Concat("editorProcess".AsByteArray());
            epKey = Hash256( epKey );

            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );

            for( int i = 0; i < processes.Length; i += 32 )
            {
                if( processes.Range( i, 32 ) == processkey )
                {
                    byte[] publishKey = processkey.Concat("Publish".AsByteArray()); ;
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


        // function string "RegisterEditor()" => "5265676973746572456469746f722829"
        // invoke example in Main: "5265676973746572456469746f722829" ["AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y"]
        // input: expected byte[] with 20 bytes (representing WIF example: b'23ba2703c53263e8d6e522dc32203339dcd8eee9')
        // output: return is expected to be 32 bytes hash: b'29ca0156b7d9c9e4592821631872e2d497a92b89ee508f0767fcec3686abc7c7'
        public static byte[] RegisterEditor( byte[] address )
        {
            byte[] editorAddress = address;

            if (!Runtime.CheckWitness(editorAddress)) return null;

            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
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

        // function string "RegisterReviewer()" => "526567697374657252657669657765722829"
        // invoke example in Main: "526567697374657252657669657765722829" ["AK2nJJpJr6o664CWJKi1QRXjqeic2zRp8y","APLJBPhtRg2XLhtpxEHd6aRNL7YSLGH2ZL"]
        // input: expected two byte[] with 20 bytes (representing WIF example: b'23ba2703c53263e8d6e522dc32203339dcd8eee9' and b'52eaab8b2aab608902c651912db34de36e7a2b0f')
        // output: return is expected to be true (if not registered yet)
        public static bool RegisterReviewer( byte[] address, byte[] ReviewerAddress )
        {
            byte[] editorAddress = address;

            if (!Runtime.CheckWitness(editorAddress)) return false;

            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
            editorKey = Hash256( editorKey );

            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Not an Editor" );
                return false;
            }

            byte[] reviewersKey = editorAddress.Concat("reviewersAddress".AsByteArray());
            reviewersKey = Hash256( reviewersKey );

            byte[] reviewers = Storage.Get( Storage.CurrentContext, reviewersKey );
            Runtime.Notify( "Current storage for the key:" );
            Runtime.Notify( reviewersKey );
            Runtime.Notify( "is:" );
            Runtime.Notify(reviewers);


            for( int i = 0; i < reviewers.Length; i += 20 )
            {
                if( reviewers.Range( i, 20 ) == ReviewerAddress )
                {
                    Runtime.Notify( "Reviewer already registered" );
                    return false;
                }
            }

            reviewers=reviewers.Concat( ReviewerAddress );

            Storage.Put( Storage.CurrentContext, reviewersKey, reviewers );
 
            Runtime.Notify( "Reviewer registered" );
            Runtime.Notify( "This should be the next storage:" );
            Runtime.Notify(reviewers);
            Runtime.Notify("Since this addres was included:");
            Runtime.Notify(ReviewerAddress);

            /* adicionando o revisor no ranking */
            if( Storage.Get( Storage.CurrentContext, address ).Length == 0 )
                return true;
            /* lvl hash */
            byte[] lvlhash = ReviewerAddress.Concat("endorseLvl".AsByteArray());
            lvlhash = Hash256( lvlhash );

            /* hash skills count */
            byte[] skillscounthash = ReviewerAddress.Concat("endorseCount".AsByteArray());
            skillscounthash = Hash256( skillscounthash );

            /* hash skills count */
            byte[] lvlcounthash = ReviewerAddress.Concat("endorseLvlCount".AsByteArray());
            lvlcounthash = Hash256( lvlcounthash );

            byte[] endorseData = new byte[] {};
            endorseData = endorseData.Concat( lvlhash);
            //byte[] endorseData = lvlhash;
            endorseData = endorseData.Concat( skillscounthash) ;
            endorseData = endorseData.Concat( lvlcounthash );
            Storage.Put( Storage.CurrentContext, address, endorseData );

            return true;
        }

        private static bool VerifyWitness( byte[] address )
        {
            bool ok = Runtime.CheckWitness( address );
            if( !ok )
                Runtime.Notify( "You are not the address" );
            return ok;
        }

        public static bool Endorse( byte[] address, byte[] toaddress, byte[] skill )
        {
            if( !VerifyWitness( address ) )
                return false;

            if ( address == toaddress )
            {
                Runtime.Notify( "You can't endorse yourself" );
                return false;
            }

            byte[] smartContractScriptHash = ExecutionEngine.ExecutingScriptHash;

            byte[] senderData = Storage.Get( Storage.CurrentContext, address );
            byte[] receiverData = Storage.Get( Storage.CurrentContext, toaddress );

            if( senderData.Length == 0 || receiverData.Length == 0 )
            {
                Runtime.Notify( "Not a reviewer" );
                return false;
            }

            byte[] receiverSkills = Storage.Get( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 32, 32 ) ) );

            bool ok = false;
            for( int i = 0; i < receiverSkills.Length; i += 32 )
            {
                byte[] sk = receiverSkills.Range(i, 32);
                if( sk == skill )
                {
                    byte[] count = Storage.Get( Storage.CurrentContext, sk );

                    for( int j = 0; j < count.Length; j += 33 )
                    {
                        if( count.Range( i , 33 ) == address )
                        {
                            Runtime.Notify( "Already endorsed" );
                            return false;
                        }
                    }
                    count.Concat( address );
                    Storage.Put( Storage.CurrentContext, sk, count );
                    ok = true;
                    break;
                }
            }

            if( !ok )
            {
                byte[] sk = toaddress.Concat("endorseSkill".AsByteArray());
                sk = sk.Concat( skill );
                sk = Hash256( sk );
                Storage.Put( Storage.CurrentContext, sk, address );
                receiverSkills = receiverSkills.Concat( sk );
                Storage.Put( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 32, 32 ) ), receiverSkills);
            }

            byte[] receiverLvlCount = Storage.Get( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 64, 32 ) ) );
            byte[] receiverlvl = Storage.Get( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 0, 32 ) ) );
            byte[] senderlvl = Storage.Get(Storage.CurrentContext, Storage.Get( Storage.CurrentContext, senderData.Range( 0, 32 ) ) );

            if( receiverlvl.Length <= senderlvl.Length )
            {
                receiverLvlCount = receiverLvlCount.Concat( new byte[] { 0 } );// subiu um no contador de lvl
            }

            if( receiverLvlCount.Length == receiverlvl.Length )
            {
                receiverLvlCount = new byte[] { };
                receiverlvl = receiverlvl.Concat( new byte[] { 0 } );
            }

            Storage.Put( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 64, 32 ) ), receiverLvlCount );
            Storage.Put( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 0, 32 ) ), receiverlvl );

            byte[] data = Storage.Get( Storage.CurrentContext, smartContractScriptHash );
            byte[] newData = address.Concat( toaddress );
            data = data.Concat( newData );
            Storage.Put( Storage.CurrentContext, smartContractScriptHash, data );

            return true;
        }

        public static byte[] GetEndorseData( byte[] address )
        {
            byte[] data = Storage.Get( Storage.CurrentContext, address );

            if( data.Length == 0 )
            {
                return null;
            }

            return data;
        }
    }
}
