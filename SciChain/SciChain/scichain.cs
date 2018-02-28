using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Services.Neo;
using Neo.SmartContract.Framework.Services.System;
using System;
using System.Linq;

namespace Neo.SmartContract
{
    public class SciChain : Framework.SmartContract
    {
        public static object Main( string operation, params object[] args )
        {

            //verifying the operation arg
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
                return Publish((byte[])args[0], (byte[])args[1]);
            }

            if (operation == "GetPublishedData()")
            {
                if (args.Length != 2) return false;
                return GetPublishedData((byte[])args[0], (byte[])args[1], (string)args[2]);
            }

            if ( operation == "RegisterEditor()" )
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

        /*
         Get the process status:
             Not found -> 0
             Process rejected -> 1
             Waiting editor acceptance ->  2
             Waiting encrypted article ->  3
             Waiting Reviewers send grades and comments -> 4
             Waiting Editor approval -> 5
             Waiting decrypted article -> 6
             waiting reviewers approval -> 7
             Waiting for publication ->  8
             Published -> 9
         */
        public static byte GetProcessStatus( byte[] processkey )
        {
            byte[] statusKey = processkey.Concat("Status".AsByteArray());
            statusKey = Hash256(statusKey);
            byte[] status = Storage.Get(Storage.CurrentContext, statusKey);
            if (status.Length == 0)
                return 0;
            return Storage.Get(Storage.CurrentContext, statusKey)[0];
        }

        /*First steps of a manuscript submission
         recieves WIF address of main author, abstract and WIF of the editor that will handle it
         verifies if Editor is registered and returns the processKey with abstract and both publicKeys
        */ 
        public static byte[] RequestArticle( byte[] address, byte[] data, byte[] editorAddress )
        {
            byte[] authorAddress = address;

            if( !VerifyWitness( authorAddress ) ) //checking if the address is the same of caller's address
                return null;

            //calculating key with 256bits that has unique value for the editor
            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
            editorKey = Hash256(editorKey);

            Runtime.Notify("using editorKey:");
            Runtime.Notify(editorKey);

            //checking if the editor is registered
            if ( Storage.Get( Storage.CurrentContext, editorKey ) != editorAddress )
            {
                Runtime.Notify( "Editor not found" );
                return null;
            }

            //calculating key with 256bits that has unique value for all editor processes
            byte[] epKey = editorAddress.Concat("editorProcess".AsByteArray());
            epKey = Hash256(epKey);

            Runtime.Notify("using epKey:");
            Runtime.Notify(epKey);

            //getting all processes keys ( 32 bytes ) that editor has
            byte[] processes = Storage.Get( Storage.CurrentContext, epKey );
            Runtime.Notify("processes:");
            Runtime.Notify(processes);

            /*calculating key with 256bits that has unique value for the process
               using all previous processes keys as nonce*/
            byte[] processKey = Hash256(processes).Concat("Process".AsByteArray());
            processKey = processKey.Concat(editorAddress);
            processKey = processKey.Concat(authorAddress);
            processKey = Hash256(processKey);

            Runtime.Notify("final processKey:");
            Runtime.Notify(processKey);

            processes = processes.Concat( processKey );
            Storage.Put( Storage.CurrentContext, epKey, processes );

            Runtime.Notify("epKey => processes: ");
            Runtime.Notify(processes);

            //calculating key with 256bits that has unique value for the author
            byte[] authorKey = processKey.Concat("Author".AsByteArray());
            authorKey = authorKey.Concat( authorAddress );
            Runtime.Notify("authorKey before Hash" );
            Runtime.Notify(authorKey);
            authorKey = Hash256( authorKey );
            Storage.Put(Storage.CurrentContext, authorKey, authorAddress);//store the author address into authorkey

            Runtime.Notify("authorKey:");
            Runtime.Notify(authorKey);

            /*creating the process data:
                status key ( 32 byte )
                author key ( 32 bytes )
                editor key ( 32 bytes )
                process reviewers key ( 32 bytes ) - > all reviewers keys inside ( 32 bytes each )
                data key ( 32 bytes )
            */
            //calculating key with 256bits that has unique value for the process status
            byte[] statusKey = processKey.Concat("Status".AsByteArray());
            statusKey = Hash256(statusKey);
            Storage.Put(Storage.CurrentContext, statusKey, new byte[] { 2 });// first status -> Waiting editor acceptance

            //calculating key with 256bits that has unique value for the process reviewers
            byte[] processReviewersKey = processKey.Concat("ProcessReviewers".AsByteArray());
            processReviewersKey = Hash256(processReviewersKey);

            //calculating key with 256bits that has unique value for the process data
            byte[] dataKey = processKey.Concat("Data".AsByteArray());
            dataKey = Hash256(dataKey);
            Storage.Put(Storage.CurrentContext, dataKey, data);// abstract

            //calculating key with 256bits that has unique value for the last process Approval
            byte[] approvalKey = processKey.Concat("Approval".AsByteArray());
            approvalKey = Hash256(approvalKey);

            //creating the process header: 192 bytes
            byte[] processHeader = new byte[] {}; 
            processHeader = processHeader.Concat(statusKey); // byte 0 - 31
            processHeader = processHeader.Concat(authorKey); // byte 32 - 63
            processHeader = processHeader.Concat(editorKey); // byte 64 - 95
            processHeader = processHeader.Concat(processReviewersKey); // byte 96 - 127
            processHeader = processHeader.Concat( dataKey ); // byte 128 - 159
            processHeader = processHeader.Concat(approvalKey); // byte 160 - 191

            Runtime.Notify("processKey => processData: ");
            Runtime.Notify(processHeader);

            Storage.Put( Storage.CurrentContext, processKey, processHeader ); //writing the data

            return processKey;
        }

        /*
         This function is used to send data along the process. What you should send changes when the status changes.
         For example:
            if the status is 2, only the editor can send data to the process and this data must be the new status, all the revisors and the public keys
            if the status is 3, only the author can send the article encrypted by a simmetric key and the simetric key used encrypted with the public keys
            if the status is 4, only the reviewers can send the grade and the comments
        */
        public static bool SendDataToProcess( byte[] address, byte[] processkey, byte[] data )
        {
            Runtime.Notify("Data:");
            Runtime.Notify(data);

            if ( data.Length == 0 )
            {
                Runtime.Notify( "Empty data" );
                return false;
            }

            byte[] ownAddress = address;

            if( !VerifyWitness( ownAddress ) )
                return false;

            //calculating key with 256bits that has unique value for the process data
            byte[] processHeader = Storage.Get(Storage.CurrentContext, processkey);
            byte[] processData = Storage.Get(Storage.CurrentContext, processHeader.Range(128,32));
            byte status = GetProcessStatus(processkey);

            Runtime.Notify("restaured processKey => processData: ");
            Runtime.Notify(processData);
            Runtime.Notify("from processKey:");
            Runtime.Notify(processkey);

            Runtime.Notify("Current process status:");
            Runtime.Notify(status);

            if( status == 0 )
            {
                Runtime.Notify( "Can't send data to an undocumented process key" );
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
                Runtime.Notify( "Can't send data to an already published process key" );
                return false;
            }

            if ( status == 2 )
            {
                Runtime.Notify("Inside Status 2 - I");
                Runtime.Notify(status);
                //calculating key with 256bits that has unique value for the editor
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if (processHeader.Range( 64, 32 ) != editorKey ) //getting the data from the header and checking if the caller is the editor
                {
                    Runtime.Notify( "Not the article editor" );
                    return false;
                }

		        Runtime.Notify("Inside Status 2 - II" );
          	    Runtime.Notify(status);
         

                if ( data[0] == 1 || data[0] == 3 )
                {
                    Runtime.Notify("Inside Status 2 - III" );
                    Runtime.Notify(status);

                    Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), data.Range(0,1));//setting the new status

                    int reviewersaddressBytes = data[1] * 20;
                    byte[] reviewersKeys = new byte[] { };
                    for (int i = 1; i <= reviewersaddressBytes; i += 20)
                    {
                        byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                        reviewerKey = reviewerKey.Concat(data.Range(i,20));
                        reviewerKey = Hash256(reviewerKey);
                        Storage.Put(Storage.CurrentContext, reviewerKey, data.Range(i, 20));//storing reviewer address into reviewer key
                        reviewersKeys = reviewersKeys.Concat(reviewerKey);
                    }

                    Storage.Put(Storage.CurrentContext, processHeader.Range(96, 32), reviewersKeys);// writing the reviewerskeys inside the processReviewerskeys
                    Storage.Put(Storage.CurrentContext, processHeader.Range(128,32), data.Range(reviewersaddressBytes + 1, data.Length - 1));// all reviewers public keys ( generated outside the blockchain )
                    return true;
                }

                Runtime.Notify("A status data must be Rejected(1) or Waiting article(3)");
                return false;
            }

            if( status == 3 )
            {
                Runtime.Notify("Inside Status 3 - I" );
                Runtime.Notify(status);
                //calculating key with 256bits that has unique value for the author
                byte[] authorKey = processkey.Concat("Author".AsByteArray());
                authorKey = authorKey.Concat( ownAddress );
                Runtime.Notify("authorKey before Hash" );
                Runtime.Notify(authorKey);

                authorKey = Hash256( authorKey );

                Runtime.Notify("authorKey" );
                Runtime.Notify(authorKey);

                Runtime.Notify("processData.Range( 1, 32 )" );
                Runtime.Notify(processData.Range( 1, 32 ));

                if(processHeader.Range( 32, 32 ) != authorKey) //getting the data from the header and checking if the caller is the author
                {
                    Runtime.Notify( "Not the article author" );
                    return false;
                }

                Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), new byte[] { 4 } );//setting the new status
                Storage.Put(Storage.CurrentContext, processHeader.Range(128, 32), data); //sending the article encrypted by a simmetric key and the simetric key used encrypted with the public keys

                Runtime.Notify("newProcessData with Article:" );
                Runtime.Notify(data);
                return true;
            }

            if( status == 4 )
            {
                Runtime.Notify("Inside Status 4 - I" );
                Runtime.Notify(status);

                //calculating key with 256bits that has unique value for the reviewer
                byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                reviewerKey = reviewerKey.Concat( ownAddress );
                reviewerKey = Hash256( reviewerKey );

                Runtime.Notify("reviewerKey:" );
                Runtime.Notify(reviewerKey);

                byte[] reviewersKeys = Storage.Get(Storage.CurrentContext, processHeader.Range(96, 32));

                for ( int i = 0; i < reviewersKeys.Length; i += 32 )
                {
                    Runtime.Notify("i:" );
                    Runtime.Notify(i);

                    if(reviewersKeys.Range( i, 32 ) == reviewerKey)//getting the data from the header and checking if the caller is one of the reviewers
                    {
                        //calculating key with 256bits that has unique value for the reviewer to get and write the reviewer comments
                        byte[] reviewerCommentsGradesKey = processkey.Concat("ReviewerComments".AsByteArray());
                        reviewerCommentsGradesKey = reviewerCommentsGradesKey.Concat( reviewerKey );
                        reviewerCommentsGradesKey = Hash256( reviewerCommentsGradesKey );

                  	    Runtime.Notify("reviewerCommentsKey:" );
                    	Runtime.Notify(reviewerCommentsGradesKey);

                        byte[] reviewerComments = Storage.Get( Storage.CurrentContext, reviewerCommentsGradesKey );

                        if( reviewerCommentsGradesKey.Length > 0 )//checkig if the reviewer already send the grade
                        {
                            Runtime.Notify( "Your review was already registered." );
                            return false;
                        }

                        Storage.Put( Storage.CurrentContext, reviewerCommentsGradesKey, data );
                        processData = processData.Concat(reviewerCommentsGradesKey);
                        Storage.Put(Storage.CurrentContext, processHeader.Range(128,32), processData);

                        if (processData.Length == reviewersKeys.Length)
                            Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), new byte[] { 5 });

                        return true;
                    }
                }

                Runtime.Notify( "Caller is not a reviewer of this process key" );
                return false;
            }

            if( status == 5 )
            {
                //calculating key with 256bits that has unique value for the editor
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if (processHeader.Range( 64, 32 ) != editorKey)//getting the data from the header and checking if the caller is the editor
                {
                    Runtime.Notify( "Not the article editor" );
                    return false;
                }

                if ( data[0] == 1 || data[0] == 6 ) //checking if the new status sent is valid ( if the article was rejected or will be published )
                {
                    Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), data.Range(0, 1));//setting the new status
                    Storage.Delete(Storage.CurrentContext, processHeader.Range(128, 32));//cleaning all data
                    return true;
                }

                Runtime.Notify("A status data must be Rejected(1) or Waiting publishing(6)");
                return false;
            }

            if( status == 6 )
            {
                //calculating key with 256bits that has unique value for the author
                byte[] authorKey = processkey.Concat("Author".AsByteArray());
                authorKey = authorKey.Concat( ownAddress );
                authorKey = Hash256( authorKey );

                if(processHeader.Range( 32, 32 ) != authorKey)//getting the data from the header and checking if the caller is the author
                {
                    Runtime.Notify( "Not the article author" );
                    return false;
                }

                Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), new byte[] { 7 });//moving to the next step
                Storage.Put(Storage.CurrentContext, processHeader.Range(128, 32), data); // decrypted article
                return true;
            }

            if ( status == 7 )
            {
                if( data[0] == 1 || data[0] == 2)//checking if the approval data sent is valid ( if the article was rejected or accepted )
                {
                    //calculating key with 256bits that has unique value for the reviewer
                    byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                    reviewerKey = reviewerKey.Concat(ownAddress);
                    reviewerKey = Hash256(reviewerKey);

                    byte[] approvals = Storage.Get(Storage.CurrentContext, processHeader.Range(160, 32));

                    byte[] reviewersKeys = Storage.Get(Storage.CurrentContext, processHeader.Range(96, 32));
                    for (int i = 0, count = 0; i < reviewersKeys.Length; i += 32, count++)// finding the reviewer
                    {
                        if (reviewersKeys.Range(i, 32) == reviewerKey)
                        {
                            approvals = approvals.Concat(data);
                            Storage.Put(Storage.CurrentContext, processHeader.Range(160, 32), approvals);
                        }
                    }

                    if (approvals.Length * 32 == reviewersKeys.Length)// everybody set the decisions
                    {
                        Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), new byte[] { 8 }); ;//moving to the next step
                    }
                    return true;
                }

                Runtime.Notify("Data must be Rejected(1) or Aprroved(2)");
                return false;
            }

            Runtime.Notify( "Not the article reviewer" );
            return false;
        }

        /*
         This function is for all people involved with the process get the current data inside the processkey. It will change every time that the
         SendDataToProcess() function change something
         */
        public static byte[] ReceiveFromProcess( byte[] address, byte[] processkey )
        {
            byte[] ownAddress = address;

            if( !VerifyWitness( ownAddress ) )
                return null;

            //getting the process header
            byte[] processHeader = Storage.Get( Storage.CurrentContext, processkey );
            Runtime.Notify("restoring processKey => processHeader: ");
            Runtime.Notify(processHeader);

            //calculating key with 256bits that has unique value for the author
            byte[] authorKey = processkey.Concat("Author".AsByteArray());
            authorKey = authorKey.Concat( ownAddress );
            authorKey = Hash256( authorKey );
            Runtime.Notify("authorKey: ");
            Runtime.Notify(authorKey);


            if(processHeader.Range( 32, 32 ) != authorKey)//getting the data from the header and checking if the caller is the author
            {
                Runtime.Notify( "Right author key" );
                //calculating key with 256bits that has unique value for the editor
                byte[] editorKey = ownAddress.Concat("editorAddress".AsByteArray());
                editorKey = Hash256(editorKey);

                if (processHeader.Range( 64, 32 ) != editorKey)//getting the data from the header and checking if the caller is the editor
                {
                    //calculating key with 256bits that has unique value for the reviewer
                    byte[] reviewerKey = processkey.Concat("Reviewer".AsByteArray());
                    reviewerKey = reviewerKey.Concat( ownAddress );
                    reviewerKey = Hash256( reviewerKey );

                    bool ok = false;
                    byte[] reviewersKeys = Storage.Get(Storage.CurrentContext, processHeader.Range(96, 32));
                    for (int i = 0; i < reviewersKeys.Length; i += 32)// finding the reviewer
                    {
                        if (reviewersKeys.Range(i, 32) == reviewerKey)
                        {
                            ok = true;
                        }
                    }

                    if ( !ok )//if the caller is not involved with the process, it will be send a null data
                    {
                        Runtime.Notify( "Access denied" );
                        return null;
                    }
                }
            }

            return Storage.Get(Storage.CurrentContext, processHeader.Range(128,32));
        }

        /*
         This function is responsable for the publishment. Only the editor can acess.
         All the processdata is write into the publishkey
         */
        public static bool Publish(byte[] address, byte[] processkey)
        {
            if (GetProcessStatus(processkey) != 8)
            {
                Runtime.Notify("Can't publish");
                return false;
            }

            byte[] editorAddress = address;

            if (!VerifyWitness(editorAddress))
                return false;

            //calculating key with 256bits that has unique value for the editor
            byte[] editorKey = editorAddress.Concat("editorAddress".AsByteArray());
            editorKey = Hash256(editorKey);

            byte[] processHeader = Storage.Get(Storage.CurrentContext, processkey);

            //checking if the editor is registered
            if (Storage.Get(Storage.CurrentContext, processHeader.Range(64, 32)) != editorAddress)
            {
                Runtime.Notify("Not an Editor");
                return false;
            }

            //calculating key with 256bits that has unique value for all editor processes
            byte[] epKey = editorAddress.Concat("editorProcess".AsByteArray());
            epKey = Hash256(epKey);

            byte[] processes = Storage.Get(Storage.CurrentContext, epKey);

            for (int i = 0; i < processes.Length; i += 32)//checking if it's the calling editor processes
            {
                if (processes.Range(i, 32) == processkey)
                {
                    Storage.Put(Storage.CurrentContext, processHeader.Range(0, 32), new byte[] { 9 });
                    Runtime.Notify("Published");
                    return true;
                }
            }

            Runtime.Notify("Not a process of this Editor");
            return false;

        }

        /*
         This function is responsable for the publishment data. Everyone with the process key can get all the data.
         Use EditorKey to get the editor key ( 32 bytes )
         Use AuthorAddress to get the author address ( 20 bytes )
         Use ReviewersAddress to get all the reviewers address ( 20 bytes each )
         Use Paper to get the paper itself ( size unknow )
         */
        public static byte[] GetPublishedData( byte[] address, byte[] processkey, string infoRequest )
        {
            if( GetProcessStatus( processkey ) != 9 )
            {
                Runtime.Notify( "Not Published" );
                return null;
            }

            if( !VerifyWitness( address ) )
                return null;

            //getting the process header
            byte[] processHeader = Storage.Get(Storage.CurrentContext, processkey);

            if( infoRequest == "EditorKey" )
            {
                return processHeader.Range(64, 32);
            }

            if (infoRequest == "AuthorAddress")
            {
                return Storage.Get(Storage.CurrentContext, processHeader.Range(32, 32) );
            }

            if (infoRequest == "ReviewersAddress")
            {
                byte[] publishData = new byte[] { };
                byte[] reviwersKeys = Storage.Get(Storage.CurrentContext, processHeader.Range(96, 32));
                for (int i = 0; i < reviwersKeys.Length; i += 32)
                {
                    publishData = publishData.Concat(Storage.Get(Storage.CurrentContext, reviwersKeys.Range(i, 32)));
                }
                return publishData;
            }

            if (infoRequest == "Paper")
            {
                return Storage.Get(Storage.CurrentContext, processHeader.Range(128, 32));
            }

            return null;
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

            reviewers = reviewers.Concat( ReviewerAddress );

            Storage.Put( Storage.CurrentContext, reviewersKey, reviewers );
 
            Runtime.Notify( "Reviewer registered" );
            Runtime.Notify( "This should be the next storage:" );
            Runtime.Notify(reviewers);
            Runtime.Notify("Since this addres was included:");
            Runtime.Notify(ReviewerAddress);

            /* adicionando o revisor no ranking */
            if ( Storage.Get( Storage.CurrentContext, address ).Length == 0 )
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
            endorseData = endorseData.Concat( skillscounthash) ;
            endorseData = endorseData.Concat( lvlcounthash );
            Storage.Put( Storage.CurrentContext, address, endorseData );

            return true;
        }

        /*
         check if the caller is who claims to be
         */
        private static bool VerifyWitness( byte[] address )
        {
            bool ok = Runtime.CheckWitness( address );
            if( !ok )
                Runtime.Notify( "You are not the address" );
            return ok;
        }

        /*
         This function is responsable for the endorsement.
         Only reviweres can participate.
         All the skills is write into the reviewer address.
         You'll only change your level if you have been endorsed x times by reviewers with level higher or equal to yours.
         this x times is exactly your current level.
         everydoy starts with lvl 1 and it is set when the reviewer is registered.

            Endorse data:
             * levels's hash -> where to find reviewer level ( 32 bytes )
             * skill's hash -> where to find skills level ( 32 bytes )
             * counter level -> where to find all endorses from reviewers with level >= reviewer
            the count is set by the array length
         */
        public static bool Endorse( byte[] address, byte[] toaddress, byte[] skill )
        {
            if( !VerifyWitness( address ) )
                return false;

            if ( address == toaddress )// you cant endorse yourself
            {
                Runtime.Notify( "You can't endorse yourself" );
                return false;
            }

            byte[] smartContractScriptHash = ExecutionEngine.ExecutingScriptHash;

            byte[] senderData = Storage.Get( Storage.CurrentContext, address );
            byte[] receiverData = Storage.Get( Storage.CurrentContext, toaddress );

            if( senderData.Length == 0 || receiverData.Length == 0 )//cheking if the sender and the receiver is a registered reviewer
            {
                Runtime.Notify( "Not a reviewer" );
                return false;
            }

            //getting all the skills
            byte[] receiverSkills = Storage.Get( Storage.CurrentContext, Storage.Get( Storage.CurrentContext, receiverData.Range( 32, 32 ) ) );

            bool ok = false;
            for( int i = 0; i < receiverSkills.Length; i += 32 )// checking if ou already have this skill endorsed and if this address already endorsed this skill
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
                    count = count.Concat( address );
                    Storage.Put( Storage.CurrentContext, sk, count );
                    ok = true;
                    break;
                }
            }

            if( !ok )// if it's a new skill, it will be created and added into your data
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

            if( receiverlvl.Length <= senderlvl.Length )// if the sender level is higher or equal to yours it will be counted
            {
                receiverLvlCount = receiverLvlCount.Concat( new byte[] { 0 } );
            }

            if( receiverLvlCount.Length == receiverlvl.Length )// if your level counter is equal to your level will go to the next level
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

        //check some reviewers endorsements
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
