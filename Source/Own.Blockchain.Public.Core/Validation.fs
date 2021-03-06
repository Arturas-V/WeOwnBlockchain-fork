﻿namespace Own.Blockchain.Public.Core

open System.Text.RegularExpressions
open Own.Common.FSharp
open Own.Blockchain.Common
open Own.Blockchain.Public.Core.DomainTypes
open Own.Blockchain.Public.Core.Dtos

module Validation =

    let private validateHash isValidHash (hashValue : string) propertyName =
        [
            if hashValue.IsNullOrWhiteSpace() then
                yield AppError (sprintf "%s is not provided" propertyName)
            elif hashValue <> hashValue.Trim() then
                yield AppError (sprintf "%s contains leading/trailing spaces" propertyName)
            else
                if not (isValidHash hashValue) then
                    yield AppError (sprintf "%s is not a valid SHA256 hash" propertyName)
        ]

    let private validateNetworkAddress (networkAddress : string) =
        [
            if networkAddress.IsNullOrWhiteSpace() then
                yield AppError "NetworkAddress is not provided"
            if networkAddress <> networkAddress.Trim() then
                yield AppError "NetworkAddress contains leading/trailing spaces"
        ]

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // Block validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let validateBlock isValidHash isValidAddress (blockDto : BlockDto) =
        [
            if blockDto.Header.Number < 0L then
                yield AppError "Block.Header.Number cannot be negative"

            yield! validateHash isValidHash blockDto.Header.Hash "Block.Header.Hash"

            yield! validateHash isValidHash blockDto.Header.PreviousHash "Block.Header.PreviousHash"

            if blockDto.Header.ConfigurationBlockNumber < 0L then
                yield AppError "Block.Header.ConfigurationBlockNumber cannot be negative"

            if blockDto.Header.Timestamp < 0L then
                yield AppError "Block.Header.Timestamp cannot be negative"
            if blockDto.Header.Timestamp > Utils.getNetworkTimestamp () then
                yield AppError "Block.Header.Timestamp cannot be in future"

            if blockDto.Header.ProposerAddress.IsNullOrWhiteSpace() then
                yield AppError "Block.Header.ProposerAddress is missing"
            elif blockDto.Header.ProposerAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "Block.Header.ProposerAddress is not valid"
        ]
        |> Errors.orElseWith (fun _ -> Mapping.blockFromDto blockDto)

    let validateBlockEnvelope
        isValidHash
        isValidAddress
        (blockEnvelopeDto : BlockEnvelopeDto)
        : Result<BlockEnvelope, AppErrors>
        =

        [
            match validateBlock isValidHash isValidAddress blockEnvelopeDto.Block with
            | Ok _ ->
                if blockEnvelopeDto.Signatures.IsEmpty then
                    yield AppError "Signatures are missing from the block envelope"
            | Error errors -> yield! errors
        ]
        |> Errors.orElseWith (fun _ -> Mapping.blockEnvelopeFromDto blockEnvelopeDto)

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TxAction validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private validateTransferChx isValidAddress (action : TransferChxTxActionDto) =
        [
            if action.RecipientAddress.IsNullOrWhiteSpace() then
                yield AppError "RecipientAddress is not provided"
            elif action.RecipientAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "RecipientAddress is not valid"

            if action.Amount <= 0m then
                yield AppError "CHX amount must be greater than zero"

            if action.Amount > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "CHX amount cannot be greater than %M" Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 action.Amount) then
                yield AppError "CHX amount must have at most 7 decimals"
        ]

    let private validateTransferAsset isValidHash (action : TransferAssetTxActionDto) =
        [
            yield! validateHash isValidHash action.FromAccountHash "FromAccountHash"

            yield! validateHash isValidHash action.ToAccountHash "ToAccountHash"

            if action.ToAccountHash = action.FromAccountHash then
                yield AppError "ToAccountHash cannot be the same as FromAccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"

            if action.Amount <= 0m then
                yield AppError "Asset amount must be greater than zero"

            if action.Amount > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "Asset amount cannot be greater than %M" Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 action.Amount) then
                yield AppError "Asset amount must have at most 7 decimals"
        ]

    let private validateCreateAssetEmission isValidHash (action : CreateAssetEmissionTxActionDto) =
        [
            yield! validateHash isValidHash action.EmissionAccountHash "EmissionAccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"

            if action.Amount <= 0m then
                yield AppError "Asset amount must be greater than zero"

            if action.Amount > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "Asset amount cannot be greater than %M" Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 action.Amount) then
                yield AppError "Asset amount must have at most 7 decimals"
        ]

    let private validateSetAccountController isValidHash isValidAddress (action : SetAccountControllerTxActionDto) =
        [
            yield! validateHash isValidHash action.AccountHash "AccountHash"

            if action.ControllerAddress.IsNullOrWhiteSpace() then
                yield AppError "ControllerAddress is not provided"
            elif action.ControllerAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "ControllerAddress is not valid"
        ]

    let private validateSetAssetController isValidHash isValidAddress (action : SetAssetControllerTxActionDto) =
        [
            yield! validateHash isValidHash action.AssetHash "AssetHash"

            if action.ControllerAddress.IsNullOrWhiteSpace() then
                yield AppError "ControllerAddress is not provided"
            elif action.ControllerAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "ControllerAddress is not valid"
        ]

    let private validateSetAssetCode isValidHash (action : SetAssetCodeTxActionDto) =
        [
            yield! validateHash isValidHash action.AssetHash "AssetHash"

            if action.AssetCode.IsNullOrWhiteSpace() then
                yield AppError "AssetCode is not provided"

            if action.AssetCode.Length > 20 then
                yield AppError "Asset code cannot be longer than 20 chars"

            if not (Regex.IsMatch(action.AssetCode, @"^[0-9A-Z]+$")) then
                yield AppError "Asset code can only contain digits and upper case letters"
        ]

    let private validateConfigureValidator (action : ConfigureValidatorTxActionDto) =
        [
            yield! validateNetworkAddress action.NetworkAddress

            if action.SharedRewardPercent < 0m then
                yield AppError "SharedRewardPercent cannot be negative"

            if action.SharedRewardPercent > 100m then
                yield AppError "SharedRewardPercent cannot be greater than 100"

            if not (Utils.isRounded2 action.SharedRewardPercent) then
                yield AppError "Shared reward percent must have at most 2 decimals"
        ]

    let private validateDelegateStake isValidAddress (action : DelegateStakeTxActionDto) =
        [
            if action.ValidatorAddress.IsNullOrWhiteSpace() then
                yield AppError "ValidatorAddress is not provided"
            elif action.ValidatorAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "ValidatorAddress is not valid"

            if action.Amount = 0m then
                yield AppError "CHX amount cannot be zero"

            if action.Amount > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "CHX amount cannot be greater than %M" Utils.maxBlockchainNumeric)
            if action.Amount < -Utils.maxBlockchainNumeric then
                yield AppError (sprintf "CHX amount cannot be less than %M" -Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 action.Amount) then
                yield AppError "CHX amount must have at most 7 decimals"
        ]

    let private validateSubmitVote isValidHash (action : SubmitVoteTxActionDto) =
        [
            yield! validateHash isValidHash action.AccountHash "AccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"

            yield! validateHash isValidHash action.ResolutionHash "ResolutionHash"

            yield! validateHash isValidHash action.VoteHash "VoteHash"
        ]

    let private validateSubmitVoteWeight isValidHash (action : SubmitVoteWeightTxActionDto) =
        [
            yield! validateHash isValidHash action.AccountHash "AccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"

            yield! validateHash isValidHash action.ResolutionHash "ResolutionHash"

            if action.VoteWeight < 0m then
                yield AppError "Vote weight cannot be negative"

            if action.VoteWeight > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "Vote weight cannot be greater than %M" Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 action.VoteWeight) then
                yield AppError "Vote weight must have at most 7 decimals"
        ]

    let private validateSetAccountEligibility isValidHash (action : SetAccountEligibilityTxActionDto) =
        [
            yield! validateHash isValidHash action.AccountHash "AccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"
        ]

    let private validateSetAssetEligibility isValidHash (action : SetAssetEligibilityTxActionDto) =
        [
            yield! validateHash isValidHash action.AssetHash "AssetHash"
        ]

    let private validateSetKycProvider
        isValidHash
        isValidAddress
        (assetHash : string)
        (providerAddress : string)
        =

        [
            yield! validateHash isValidHash assetHash "AssetHash"

            if providerAddress.IsNullOrWhiteSpace() then
                yield AppError "KYC Provider Address is not provided"
            elif providerAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "KYC Provider Address is not valid"
        ]

    let private validateChangeKycControllerAddress
        isValidHash
        isValidAddress
        (action : ChangeKycControllerAddressTxActionDto)
        =

        [
            yield! validateHash isValidHash action.AccountHash "AccountHash"

            yield! validateHash isValidHash action.AssetHash "AssetHash"

            if action.KycControllerAddress.IsNullOrWhiteSpace() then
                yield AppError "ValidatorAddress is not provided"
            elif action.KycControllerAddress |> BlockchainAddress |> isValidAddress |> not then
                yield AppError "ValidatorAddress is not valid"
        ]

    let private validateAddKycProvider isValidHash isValidAddress (action : AddKycProviderTxActionDto) =
        validateSetKycProvider isValidHash isValidAddress action.AssetHash action.ProviderAddress

    let private validateRemoveKycProvider isValidHash isValidAddress (action : RemoveKycProviderTxActionDto) =
        validateSetKycProvider isValidHash isValidAddress action.AssetHash action.ProviderAddress

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // TX validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let private validateTxFields maxActionCountPerTx (BlockchainAddress signerAddress) (t : TxDto) =
        [
            if t.SenderAddress <> signerAddress then
                yield AppError "Sender address doesn't match the signature"

            if t.Nonce <= 0L then
                yield AppError "Nonce must be greater than zero"

            if t.ExpirationTime < 0L then
                yield AppError "ExpirationTime cannot be negative"

            if t.ActionFee <= 0m then
                yield AppError "ActionFee must be greater than zero"

            if t.ActionFee > Utils.maxBlockchainNumeric then
                yield AppError (sprintf "ActionFee cannot be greater than %M" Utils.maxBlockchainNumeric)

            if not (Utils.isRounded7 t.ActionFee) then
                yield AppError "ActionFee must have at most 7 decimal places"

            if t.Actions.IsEmpty then
                yield AppError "There are no actions provided for this transaction"
            elif t.Actions.Length > maxActionCountPerTx then
                yield AppError (sprintf "Max allowed number of actions per transaction is %i" maxActionCountPerTx)
        ]

    let private validateTxActions isValidHash isValidAddress (actions : TxActionDto list) =
        let validateTxAction (action : TxActionDto) =
            match action.ActionData with
            | :? TransferChxTxActionDto as a ->
                validateTransferChx isValidAddress a
            | :? TransferAssetTxActionDto as a ->
                validateTransferAsset isValidHash a
            | :? CreateAssetEmissionTxActionDto as a ->
                validateCreateAssetEmission isValidHash a
            | :? CreateAccountTxActionDto ->
                [] // Nothing to validate.
            | :? CreateAssetTxActionDto ->
                [] // Nothing to validate.
            | :? SetAccountControllerTxActionDto as a ->
                validateSetAccountController isValidHash isValidAddress a
            | :? SetAssetControllerTxActionDto as a ->
                validateSetAssetController isValidHash isValidAddress a
            | :? SetAssetCodeTxActionDto as a ->
                validateSetAssetCode isValidHash a
            | :? ConfigureValidatorTxActionDto as a ->
                validateConfigureValidator a
            | :? RemoveValidatorTxActionDto ->
                [] // Nothing to validate
            | :? DelegateStakeTxActionDto as a ->
                validateDelegateStake isValidAddress a
            | :? SubmitVoteTxActionDto as a ->
                validateSubmitVote isValidHash a
            | :? SubmitVoteWeightTxActionDto as a ->
                validateSubmitVoteWeight isValidHash a
            | :? SetAccountEligibilityTxActionDto as a ->
                validateSetAccountEligibility isValidHash a
            | :? SetAssetEligibilityTxActionDto as a ->
                validateSetAssetEligibility isValidHash a
            | :? ChangeKycControllerAddressTxActionDto as a ->
                validateChangeKycControllerAddress isValidHash isValidAddress a
            | :? AddKycProviderTxActionDto as a ->
                validateAddKycProvider isValidHash isValidAddress a
            | :? RemoveKycProviderTxActionDto as a ->
                validateRemoveKycProvider isValidHash isValidAddress a
            | _ ->
                let error = sprintf "Unknown action data type: %s" (action.ActionData.GetType()).FullName
                [AppError error]

        actions
        |> List.collect validateTxAction

    let validateTx isValidHash isValidAddress maxActionCountPerTx sender hash (txDto : TxDto) : Result<Tx, AppErrors> =
        validateTxFields maxActionCountPerTx sender txDto
        @ validateTxActions isValidHash isValidAddress txDto.Actions
        |> Errors.orElseWith (fun _ -> Mapping.txFromDto sender hash txDto)

    let checkIfBalanceCanCoverFees
        (getAvailableBalance : BlockchainAddress -> ChxAmount)
        getTotalFeeForPendingTxs
        senderAddress
        totalTxFee
        : Result<unit, AppErrors>
        =

        let availableBalance = getAvailableBalance senderAddress

        if totalTxFee > availableBalance then
            Result.appError "Available CHX balance is insufficient to cover the fee"
        else
            let totalFeeForPendingTxs = getTotalFeeForPendingTxs senderAddress

            if (totalFeeForPendingTxs + totalTxFee) > availableBalance then
                Result.appError "Available CHX balance is insufficient to cover the fee for all pending transactions"
            else
                Ok ()

    let validateTxEnvelope (txEnvelopeDto : TxEnvelopeDto) : Result<TxEnvelope, AppErrors> =
        [
            if txEnvelopeDto.Tx.IsNullOrWhiteSpace() then
                yield AppError "TX is missing from the TX envelope"
            if txEnvelopeDto.Signature.IsNullOrWhiteSpace() then
                yield AppError "Signature is missing from the TX envelope"
        ]
        |> Errors.orElseWith (fun _ -> Mapping.txEnvelopeFromDto txEnvelopeDto)

    let verifyTxSignature createHash verifySignature (txEnvelope : TxEnvelope) : Result<BlockchainAddress, AppErrors> =
        let txHash = createHash txEnvelope.RawTx
        match verifySignature txEnvelope.Signature txHash with
        | Some blockchainAddress ->
            Ok blockchainAddress
        | None ->
            Result.appError "Cannot verify TX signature"

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    // EquivocationProof validation
    ////////////////////////////////////////////////////////////////////////////////////////////////////

    let verifyEquivocationProofSignature
        (verifySignature : Signature -> string -> BlockchainAddress option)
        createConsensusMessageHash
        blockNumber
        consensusRound
        consensusStep
        (blockHash : string)
        signature
        =

        let consensusMessage =
            match consensusStep with
            | 0uy -> failwith "Equivocation is not checked on Propose messages"
            | 1uy -> blockHash |> Option.ofObj |> Option.map BlockHash |> Vote
            | 2uy -> blockHash |> Option.ofObj |> Option.map BlockHash |> Commit
            | c -> failwithf "Unknown consensus step code: %i" c

        let consensusMessageHash =
            createConsensusMessageHash
                (blockNumber |> BlockNumber)
                (consensusRound |> ConsensusRound)
                consensusMessage

        verifySignature (Signature signature) consensusMessageHash

    let validateEquivocationProof
        (verifySignature : Signature -> string -> BlockchainAddress option)
        createConsensusMessageHash
        decodeHash
        createHash
        (equivocationProofDto : EquivocationProofDto)
        : Result<EquivocationProof, AppErrors>
        =

        if equivocationProofDto.EquivocationValue1 = equivocationProofDto.EquivocationValue2 then
            Result.appError "Values in equivocation proof must differ"
        elif equivocationProofDto.EquivocationValue1 > equivocationProofDto.EquivocationValue2 then
            // This is not expected to happen for honest nodes, due to the ConsensusState.CreateEquivocationProof logic.
            Result.appError "Values in equivocation proof must be ordered (v1 < v2) to prevent double slashing"
        else
            let signer1 =
                verifyEquivocationProofSignature
                    verifySignature
                    (createConsensusMessageHash decodeHash createHash)
                    equivocationProofDto.BlockNumber
                    equivocationProofDto.ConsensusRound
                    equivocationProofDto.ConsensusStep
                    equivocationProofDto.EquivocationValue1
                    equivocationProofDto.Signature1
            let signer2 =
                verifyEquivocationProofSignature
                    verifySignature
                    (createConsensusMessageHash decodeHash createHash)
                    equivocationProofDto.BlockNumber
                    equivocationProofDto.ConsensusRound
                    equivocationProofDto.ConsensusStep
                    equivocationProofDto.EquivocationValue2
                    equivocationProofDto.Signature2

            match signer1, signer2 with
            | None, _ ->
                Result.appError "Cannot verify signature 1"
            | _, None ->
                Result.appError "Cannot verify signature 2"
            | Some s1, Some s2 ->
                if s1 <> s2 then
                    sprintf "Signatures are not from the same address (%s / %s)" s1.Value s2.Value
                    |> Result.appError
                else
                    let validatorAddress = s1

                    let equivocationValue1Bytes =
                        equivocationProofDto.EquivocationValue1
                        |> Mapping.equivocationValueFromString
                        |> Mapping.equivocationValueToBytes decodeHash
                    let equivocationValue2Bytes =
                        equivocationProofDto.EquivocationValue2
                        |> Mapping.equivocationValueFromString
                        |> Mapping.equivocationValueToBytes decodeHash

                    let equivocationProofHash =
                        [
                            equivocationProofDto.BlockNumber |> Conversion.int64ToBytes
                            equivocationProofDto.ConsensusRound |> Conversion.int32ToBytes
                            [| equivocationProofDto.ConsensusStep |]
                            equivocationValue1Bytes
                            equivocationValue2Bytes
                            equivocationProofDto.Signature1 |> decodeHash
                            equivocationProofDto.Signature2 |> decodeHash
                        ]
                        |> Array.concat
                        |> createHash
                        |> EquivocationProofHash

                    {
                        EquivocationProofHash = equivocationProofHash
                        ValidatorAddress = validatorAddress
                        BlockNumber = equivocationProofDto.BlockNumber |> BlockNumber
                        ConsensusRound = equivocationProofDto.ConsensusRound |> ConsensusRound
                        ConsensusStep = equivocationProofDto.ConsensusStep |> Mapping.consensusStepFromCode
                        EquivocationValue1 =
                            equivocationProofDto.EquivocationValue1 |> Mapping.equivocationValueFromString
                        EquivocationValue2 =
                            equivocationProofDto.EquivocationValue2 |> Mapping.equivocationValueFromString
                        Signature1 = equivocationProofDto.Signature1 |> Signature
                        Signature2 = equivocationProofDto.Signature2 |> Signature
                    }
                    |> Ok
