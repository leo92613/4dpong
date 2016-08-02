﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using ProtoBuf;
using update_protocol_v3;
using System.Threading;

namespace Holojam.Network {
	public class HolojamNetwork : Singleton<HolojamNetwork> {

		//Editor debugging
		public int sentWarning = -1, receivedWarning = 48;
		public int sentPPS;
		public List<int> receivedPPS;

		[System.NonSerialized]
		public int sentPacketsPerSecond;
		[System.NonSerialized]
		public List<int> receivedPacketsPerSecond;

		//Constant and Read-only
		public const int HOLOJAM_MOTIVE_PORT = 1611; //Port for receiving motive information
		public const int HOLOJAM_NONMOTIVE_PORT = 1612; // Port for receiving non-motive information
		public const int BLACK_BOX_SERVER_PORT = 1615; //Port for sending information

		private HolojamSendThread sendThread;
		private List<HolojamRecieveThread> receiveThreads;

		void Start() {
			receivedPacketsPerSecond = new List<int> ();
			
			sendThread = new HolojamSendThread(BLACK_BOX_SERVER_PORT);
			receiveThreads = new List<HolojamRecieveThread> ();
			receivedPPS = new List<int> ();
			AddReceiveThread(HOLOJAM_MOTIVE_PORT);
			AddReceiveThread(HOLOJAM_NONMOTIVE_PORT);

			sendThread.Start();
			foreach (HolojamThread thread in receiveThreads) {
				thread.Start();
			}

			StartCoroutine(DisplayPacketsPerSecond());
		}

		void AddReceiveThread(int port) {
			receiveThreads.Add(new HolojamRecieveThread(port));
			receivedPacketsPerSecond.Add (0);
			receivedPPS.Add (0);
		}

		void FixedUpdate() {
			List<HolojamView> viewsToSend = new List<HolojamView>();

			foreach (HolojamView view in HolojamView.instances) {
				
				if (view.IsMine) {
					viewsToSend.Add(view);
				} else {
					if (string.IsNullOrEmpty(view.Label)) {
						Debug.LogWarning("Warning: No HolojamView label on object: " + view.name);
						continue;
					}
					
					HolojamObject o;
					foreach (HolojamThread thread in receiveThreads) {
						if (thread.GetObject (view.Label, out o, Time.deltaTime)) {
							view.RawPosition = o.position;
							view.RawRotation = o.rotation;
							view.Bits = o.bits;
							view.Blob = o.blob;
							view.IsTracked = o.isTracked;
							break;
						}
						else view.IsTracked = false;
					}
				}
			}

			sendThread.UpdateManagedObjects(viewsToSend.ToArray());
		}

		private IEnumerator DisplayPacketsPerSecond() {
			bool running = true;
			foreach (HolojamThread thread in receiveThreads) {
				running = running && thread.IsRunning;
			}
			while (running) {
				yield return new WaitForSeconds(1f);

				sentPacketsPerSecond = sendThread.PacketCount;
				sendThread.PacketCount = 0;
				sentPPS = sentPacketsPerSecond;

				if (Time.frameCount > 0 && sentPPS <= sentWarning) {
					Debug.LogWarning (
						"HolojamNetwork: Sent Packets - " + sentPPS
					);
				}
				int threadIndex = 0;
				foreach (HolojamThread receiveThread in receiveThreads) {
					receivedPacketsPerSecond[threadIndex] = receiveThread.PacketCount;
					receiveThread.PacketCount = 0;
					receivedPPS[threadIndex] = receivedPacketsPerSecond[threadIndex];
					
					if (Time.frameCount > 0 && receivedPPS[threadIndex] <= receivedWarning) {
						Debug.LogWarning (
							"HolojamNetwork: Received Packets (Thread " +
							(threadIndex+1) + ") - " + receivedPPS[threadIndex]
						);
					}
					
					threadIndex++;
				}
			}
		}

		protected override void OnDestroy () {
			base.OnDestroy ();
			sendThread.Stop ();
			foreach (HolojamThread thread in receiveThreads) {
				thread.Stop ();
			}
		}
	}


	internal abstract class HolojamThread {

		protected Thread thread;

		protected int port;
		protected Dictionary<string, HolojamObject> managedObjects = new Dictionary<string, HolojamObject>();
		protected UnityEngine.Object lockObject = new UnityEngine.Object();
		protected int packetCount = 0;
		protected bool isRunning = false;
		
		protected float timer = 0;
		private const float timeout = 0.4f; //Seconds

		protected abstract ThreadStart ThreadStart {
			get;
		}

		public int PacketCount {
			get { return packetCount; }
			set { packetCount = value; }
		}

		public bool IsRunning {
			get { return isRunning; }
		}

		protected HolojamThread(int port) {
			this.port = port;
			thread = new Thread(ThreadStart);
		}

		public void Start() {
			if (this.isRunning) {
				Debug.LogWarning("HolojamNetwork: Thread already started!");
				return;
			}

			isRunning = true;
			thread.Start();
		}

		public void Stop() {
			if (!this.isRunning) {
				Debug.LogWarning("Thread already stopped!");
				return;
			}
			isRunning = false;
		}

		public bool GetObject(string key, out HolojamObject holoObject, float delta) {
			holoObject = null;
			lock (lockObject) {
				if (managedObjects.ContainsKey(key)) {
					holoObject = managedObjects[key];
					
					//If packets haven't been received in awhile, reset the tracking flag
					timer+=delta;
					if(timer>timeout){
						managedObjects[key].isTracked=false;
						//Debug.LogWarning("Packet timeout!");
					}
					
					return true;
				} else {
					return false;
				}
			}
		}
	}

	internal class HolojamRecieveThread : HolojamThread {

		private PacketBuffer previousPacket = new PacketBuffer(PacketBuffer.PACKET_SIZE);
		private PacketBuffer currentPacket = new PacketBuffer(PacketBuffer.PACKET_SIZE);
		private PacketBuffer tempPacket = new PacketBuffer(PacketBuffer.PACKET_SIZE);
		private update_protocol_v3.Update update;

		protected override ThreadStart ThreadStart {
			get {
				return Receive;
			}
		}

		public HolojamRecieveThread(int port) : base(port) { }

		public void Receive() {
			Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.Bind(new IPEndPoint(IPAddress.Any, port));
			socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, 
								   new MulticastOption(IPAddress.Parse("224.1.1.1")));

			int nBytesReceived = 0;
			while (isRunning) {
				
				nBytesReceived = socket.Receive(currentPacket.bytes);
				timer = 0; //Reset packet timer--we received packets!
				currentPacket.stream.Position = 0;

				update = Serializer.Deserialize<update_protocol_v3.Update>(
							new MemoryStream(currentPacket.bytes, 0, nBytesReceived)
							);

				currentPacket.frame = update.mod_version;
				if (currentPacket.frame > previousPacket.frame) {
					packetCount++;
					
					previousPacket.stream.Position = 0;
					currentPacket.stream.Position = 0;
					tempPacket.copyFrom(previousPacket);
					previousPacket.copyFrom(currentPacket);
					currentPacket.copyFrom(tempPacket);
					lock (lockObject) {
						managedObjects.Clear();
						
						for (int j = 0; j < update.live_objects.Count; j++) {
							LiveObject or = update.live_objects[j];
							string label = or.label;

							HolojamObject ho;

							//Reform managedObjects every frame.
							//Inefficient for now, but will allow us to determine
							//if an object is registered or not.

							ho = new HolojamObject(label);
							managedObjects[label] = ho;

							if (update.lhs_frame) {
								ho.position = new Vector3(-(float)or.x, (float)or.y, (float)or.z);
								ho.rotation = new Quaternion(-(float)or.qx,
															  (float)or.qy, 
															  (float)or.qz, 
															 -(float)or.qw);
							} else {
								ho.position = new Vector3((float)or.x, (float)or.y, (float)or.z);
								ho.rotation = new Quaternion((float)or.qx, 
															 (float)or.qy, 
									       					 (float)or.qz, 
															 (float)or.qw);
							}
							ho.bits = or.button_bits;

							//Get blob if it's there. Inefficient
							ho.blob = or.extra_data;
							
							ho.isTracked = or.is_tracked;
						}
					}
				}
				
				if (!isRunning) {
					socket.Close();
					break;
				}
			}
		}
	}

	internal class HolojamSendThread : HolojamThread {

		private int lastLoadedFrame;
		private byte[] packetBytes;
		private IPAddress ip = IPAddress.Any;
		private update_protocol_v3.Update update;

		protected override ThreadStart ThreadStart {
			get {
				return Send;
			}
		}

		public HolojamSendThread(int port) : base(port) { }

		public void Send() {
			Debug.Log("Attempting to open send thread with ip/port: " + ip.ToString() + " " + port);
			Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			IPEndPoint ipEndPoint = new IPEndPoint(ip, 0);
			IPEndPoint send_ipEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.44"), port);

			try {
				socket.Bind(ipEndPoint);
			} catch (SocketException e) {
				Debug.Log("Error binding socket: " + ip.ToString() + " " + port + " " + e.ToString());
				isRunning = false;
			}

			while (isRunning) {
				System.Threading.Thread.Sleep(10);
				if (managedObjects.Values.Count == 0)
					continue;
				lock (lockObject) {

					update = new update_protocol_v3.Update();
					update.label = "SendData";
					update.mod_version = lastLoadedFrame;
					update.lhs_frame = false;
					lastLoadedFrame++;
					
					foreach (KeyValuePair<string, HolojamObject> entry in managedObjects) {
						LiveObject o = entry.Value.ToLiveObject();
						update.live_objects.Add(o);
					}
					using (MemoryStream stream = new MemoryStream()) {
						packetCount++;
						Serializer.Serialize<Update>(stream, update);
						packetBytes = stream.GetBuffer();
						socket.SendTo(packetBytes, send_ipEndPoint);
					}
				}

				if (!isRunning) {
					socket.Close();
					break;
				}
			}
		}

		public void UpdateManagedObjects(HolojamView[] views) {
			lock (lockObject) {
				managedObjects.Clear();

				foreach (HolojamView view in views) {
					HolojamObject o = HolojamObject.FromView(view);
					managedObjects[o.label] = o;
				}
			}
		}
	}

	internal class HolojamObject {
		public static readonly Vector3 DEFAULT_POSITION = Vector3.zero;
		public static readonly Quaternion DEFAULT_ROTATION = Quaternion.identity;

		public string label;
		public Vector3 position = DEFAULT_POSITION;
		public Quaternion rotation = DEFAULT_ROTATION;
		public int bits = 0;
		public string blob = "";
		public bool isTracked = false;

		public HolojamObject(string label) {
			this.label = label;
		}

		public update_protocol_v3.LiveObject ToLiveObject() {
			update_protocol_v3.LiveObject o = new update_protocol_v3.LiveObject();
			o.label = this.label;

			o.x = position.x;
			o.y = position.y;
			o.z = position.z;

			o.qx = rotation.x;
			o.qy = rotation.y;
			o.qz = rotation.z;
			o.qw = rotation.w;

			o.button_bits = bits;

			if (!string.IsNullOrEmpty(blob)) {
				o.extra_data = blob;
			}
			
			o.is_tracked = isTracked;

			return o;
		}

		public static HolojamObject FromView(HolojamView view) {
			HolojamObject o = new HolojamObject(view.Label);

			o.position = view.RawPosition;
			o.rotation = view.RawRotation;
			o.bits = view.Bits;
			o.blob = view.Blob;
			o.isTracked = view.IsTracked;

			return o;
		}
	}
}
