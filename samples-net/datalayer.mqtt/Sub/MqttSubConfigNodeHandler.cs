/*
 * SPDX-FileCopyrightText: Bosch Rexroth AG
 *
 * SPDX-License-Identifier: MIT
 */

using Datalayer;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Samples.Datalayer.MQTT.Base;
using Samples.Datalayer.MQTT.Client;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Datalayer.MQTT.Sub
{
    /// <summary>
    /// Handler for Sub configuration node
    /// </summary>
    internal class MqttSubConfigNodeHandler : MqttSubBaseNodeHandler
    {
        /// <summary>
        /// Creates the handler
        /// </summary>
        /// <param name="root"></param>
        /// <param name="parent"></param>
        /// <param name="name"></param>
        public MqttSubConfigNodeHandler(MqttRootNodeHandler root, MqttBaseNodeHandler parent, string name) :
            base(root, parent, name)
        {

        }

        #region Overrides

        /// <summary>
        /// Starts the handler
        /// </summary>
        /// <returns></returns>
        public override DLR_RESULT Start()
        {
            //Create, register and add the handled nodes here
            //Folder (self)
            var (result, node) = Root.Provider.CreateBranchNode(this, BaseAddress, Name, true);
            if (result.IsBad())
            {
                return DLR_RESULT.DL_FAILED;
            }
            Nodes.Add(node.Address, node);

            //...

            //topic
            (result, node) = Root.Provider.CreateVariableNode(this, FullAddress, Names.Topic, new Variant(MqttClientWrapper.DefaultTopic));
            if (result.IsBad())
            {
                return DLR_RESULT.DL_FAILED;
            }
            Nodes.Add(node.Address, node);

            //target-address
            (result, node) = Root.Provider.CreateVariableNode(this, FullAddress, Names.TargetAddress, new Variant(Root.Test.Dummy1));
            if (result.IsBad())
            {
                return DLR_RESULT.DL_FAILED;
            }
            Nodes.Add(node.Address, node);

            //json-data-type
            (result, node) = Root.Provider.CreateVariableNode(this, FullAddress, Names.JsonDataType, new Variant("float"));
            if (result.IsBad())
            {
                return DLR_RESULT.DL_FAILED;
            }
            Nodes.Add(node.Address, node);

            //quality-of-service
            (result, node) = Root.Provider.CreateVariableNode(this, FullAddress, Names.QualityOfService, new Variant((int)MqttQualityOfServiceLevel.AtLeastOnce));
            if (result.IsBad())
            {
                return DLR_RESULT.DL_FAILED;
            }
            Nodes.Add(node.Address, node);

            return base.Start();
        }

        /// <summary>
        /// Returns the Topic
        /// </summary>
        protected override string Topic => GetNode(Names.Topic).Value.ToString();

        /// <summary>
        /// Resturns the Quality
        /// </summary>
        protected override MqttQualityOfServiceLevel Quality => (MqttQualityOfServiceLevel)GetNode(Names.Topic).Value.ToInt32();

        /// <summary>
        /// OnWrite handler
        /// </summary>
        /// <param name="address"></param>
        /// <param name="writeValue"></param>
        /// <param name="result"></param>
        public override void OnWrite(string address, IVariant writeValue, IProviderNodeResult result)
        {
            //Fetch the node
            if (!Nodes.TryGetValue(address, out ProviderNodeWrapper wrappedNode))
            {
                result.SetResult(DLR_RESULT.DL_FAILED);
                return;
            }

            //Check for read-only nodes
            if (wrappedNode.IsReadOnly)
            {
                result.SetResult(DLR_RESULT.DL_FAILED);
                return;
            }

            //We're only interested in changed values
            var trimmedWriteValue = writeValue.Trim();
            if (wrappedNode.Value == trimmedWriteValue)
            {
                result.SetResult(DLR_RESULT.DL_OK);
                return;
            }

            switch (wrappedNode.Name)
            {
                //topic
                case Names.Topic:
                    if (writeValue.IsEmptyOrWhiteSpace())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Unsubscribe from current MQTT topic (sync)
                    if (UnsubscribeMqttAsync().Result.IsBad())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Apply changes
                    wrappedNode.Value = trimmedWriteValue;

                    //Subscribe to new MQTT topic (sync)
                    if (SubscribeMqttAsync().Result.IsBad())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }
                    break;

                //target-address
                case Names.TargetAddress:
                    if (writeValue.IsEmptyOrWhiteSpace())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Apply changes
                    wrappedNode.Value = trimmedWriteValue;
                    break;


                //json-datatype
                case Names.JsonDataType:
                    if (writeValue.IsEmptyOrWhiteSpace())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Apply changes
                    wrappedNode.Value = trimmedWriteValue;
                    break;

                //quality-of-service
                case Names.QualityOfService:
                    if (!writeValue.IsNumber)
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    if (!MqttClientWrapper.IsValidQualityOfServiceLevel(writeValue.ToInt32()))
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Unsubscribe with current MQTT QoS (sync)
                    if (UnsubscribeMqttAsync().Result.IsBad())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }

                    //Apply changes
                    wrappedNode.Value = writeValue;

                    //Subscribe with new MQTT QoS (sync)
                    if (SubscribeMqttAsync().Result.IsBad())
                    {
                        result.SetResult(DLR_RESULT.DL_FAILED);
                        return;
                    }
                    break;
            }

            //Success
            result.SetResult(DLR_RESULT.DL_OK, writeValue);
        }

        /// <summary>
        /// OnRemove handler
        /// </summary>
        /// <param name="address"></param>
        /// <param name="result"></param>
        public override void OnRemove(string address, IProviderNodeResult result)
        {
            //Stop the handler
            if (Stop().IsBad())
            {
                result.SetResult(DLR_RESULT.DL_FAILED);
                return;
            }

            //Remove from parent
            Parent.Handlers.Remove(this);
            result.SetResult(DLR_RESULT.DL_OK);
        }

        /// <summary>
        /// MQTT message received event handler
        /// </summary>
        /// <param name="args"></param>
        protected override async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs args)
        {
            //We have to filter for our subscription to prevent duplicates if any other SUB is interested in same topic
            if (args.ApplicationMessage.SubscriptionIdentifiers[0] != SubscriptionId)
            {
                return;
            }

            //Filter for null topic which may occure
            if (args.ApplicationMessage.Topic == null)
            {
                return;
            }

            //Filter for null payload which may occure
            if (args.ApplicationMessage.PayloadSegment== null)
            {
                return;
            }

            //Filter for our topic
            var topic = GetNode(Names.Topic).Value.ToString();

            //Match (filter) for our topic
            if (MqttTopicFilterComparer.Compare(args.ApplicationMessage.Topic, topic) == MqttTopicFilterCompareResult.NoMatch)
            {
                return;
            }

            var targetAddress = GetNode(Names.TargetAddress).Value.ToString();
            var jsonDataType = GetNode(Names.JsonDataType).Value.ToString();
            var stringifiedPayload = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

            //Convert to write variant 
            var (result, writeValue) = ToVariant(jsonDataType, stringifiedPayload);
            if (result.IsBad())
            {
                Console.WriteLine($"Write topic '{topic}' failed with {result}! {stringifiedPayload} ({jsonDataType}) -> '{targetAddress}'");
                return;
            }

            //Write the Value to the ctrlX Data Layer
            var task = Root.Client.WriteAsync(targetAddress, writeValue);
            var taskResult = await task;
            if (taskResult.Result.IsBad())
            {
                Console.WriteLine($"Write topic '{topic}' failed with {taskResult.Result}! {writeValue.Value} ({writeValue.JsonDataType}) -> '{targetAddress}'");
                return;
            }

            //Success
            Console.WriteLine($"Write topic: '{topic}' -> {writeValue.Value} ({writeValue.JsonDataType}) -> '{targetAddress}'");
        }

        #endregion
    }
}
