using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;


public class SimulationTimeManager : MonoBehaviourPunCallbacks
{
    // =========================================================
    // ROOM PROPERTY KEYS
    // =========================================================

    private const string PROP_HAS_STARTED =
        "SimStarted";

    private const string PROP_IS_RUNNING =
        "SimRunning";

    private const string PROP_START_TIME =
        "SimStartTime";

    private const string PROP_PAUSED_ELAPSED =
        "SimPausedElapsed";


    // =========================================================
    // LOCAL CACHED STATE
    // =========================================================

    [SerializeField, ReadOnly]
    private bool hasStarted = false;

    [SerializeField, ReadOnly]
    private bool isRunning = false;

    [SerializeField, ReadOnly]
    private double startNetworkTime = 0;

    [SerializeField, ReadOnly]
    private double pausedElapsedTime = 0;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool HasStarted
    {
        get
        {
            return hasStarted;
        }
    }


    public bool IsRunning
    {
        get
        {
            return isRunning;
        }
    }


    public double ElapsedTime
    {
        get
        {
            if (!hasStarted)
            {
                return 0;
            }

            if (!isRunning)
            {
                return pausedElapsedTime;
            }

            return
                PhotonNetwork.Time -
                startNetworkTime;
        }
    }


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Start()
    {
        /*
         * NetworkManager already handles OnJoinedRoom().
         *
         * By the time this simulation scene exists,
         * the client should already be inside the room.
         *
         * We simply read the current room properties.
         */

        if (PhotonNetwork.InRoom)
        {
            LoadStateFromRoom();
        }

        if (PhotonNetwork.IsMasterClient)
        {
            StartSimulation();
        }

    }


    // =========================================================
    // START SIMULATION
    // =========================================================

    public void StartSimulation()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot start simulation because " +
                "the client is not in a Photon room."
            );

            return;
        }


        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning(
                "Only the Master Client can start " +
                "the simulation."
            );

            return;
        }


        if (hasStarted)
        {
            Debug.LogWarning(
                "Simulation has already started."
            );

            return;
        }


        double networkTime =
            PhotonNetwork.Time;


        Hashtable properties =
            new Hashtable
            {
                {
                    PROP_HAS_STARTED,
                    true
                },

                {
                    PROP_IS_RUNNING,
                    true
                },

                {
                    PROP_START_TIME,
                    networkTime
                },

                {
                    PROP_PAUSED_ELAPSED,
                    0.0
                }
            };


        PhotonNetwork.CurrentRoom
            .SetCustomProperties(
                properties
            );


        Debug.Log(
            $"Simulation start requested at " +
            $"Photon time {networkTime:F3}"
        );
    }


    // =========================================================
    // PAUSE SIMULATION
    // =========================================================

    public void PauseSimulation()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot pause simulation because " +
                "the client is not in a Photon room."
            );

            return;
        }


        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning(
                "Only the Master Client can pause " +
                "the simulation."
            );

            return;
        }


        if (!hasStarted)
        {
            Debug.LogWarning(
                "Cannot pause a simulation that " +
                "has not started."
            );

            return;
        }


        if (!isRunning)
        {
            return;
        }


        double elapsed =
            PhotonNetwork.Time -
            startNetworkTime;


        Hashtable properties =
            new Hashtable
            {
                {
                    PROP_IS_RUNNING,
                    false
                },

                {
                    PROP_PAUSED_ELAPSED,
                    elapsed
                }
            };


        PhotonNetwork.CurrentRoom
            .SetCustomProperties(
                properties
            );


        Debug.Log(
            $"Simulation pause requested at " +
            $"{elapsed:F3} simulation seconds."
        );
    }


    // =========================================================
    // RESUME SIMULATION
    // =========================================================

    public void ResumeSimulation()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot resume simulation because " +
                "the client is not in a Photon room."
            );

            return;
        }


        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning(
                "Only the Master Client can resume " +
                "the simulation."
            );

            return;
        }


        if (!hasStarted)
        {
            Debug.LogWarning(
                "Cannot resume a simulation that " +
                "has not started."
            );

            return;
        }


        if (isRunning)
        {
            return;
        }


        /*
         * Example:
         *
         * Simulation paused at 30 seconds.
         * PhotonNetwork.Time is now 100.
         *
         * newStartTime = 100 - 30 = 70
         *
         * Therefore:
         *
         * PhotonNetwork.Time - startNetworkTime
         *
         * continues from 30 seconds.
         */

        double newStartTime =
            PhotonNetwork.Time -
            pausedElapsedTime;


        Hashtable properties =
            new Hashtable
            {
                {
                    PROP_IS_RUNNING,
                    true
                },

                {
                    PROP_START_TIME,
                    newStartTime
                }
            };


        PhotonNetwork.CurrentRoom
            .SetCustomProperties(
                properties
            );


        Debug.Log(
            $"Simulation resume requested at " +
            $"{pausedElapsedTime:F3} simulation seconds."
        );
    }


    // =========================================================
    // RESET SIMULATION
    // =========================================================

    public void ResetSimulation()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Cannot reset simulation because " +
                "the client is not in a Photon room."
            );

            return;
        }


        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning(
                "Only the Master Client can reset " +
                "the simulation."
            );

            return;
        }


        Hashtable properties =
            new Hashtable
            {
                {
                    PROP_HAS_STARTED,
                    false
                },

                {
                    PROP_IS_RUNNING,
                    false
                },

                {
                    PROP_START_TIME,
                    0.0
                },

                {
                    PROP_PAUSED_ELAPSED,
                    0.0
                }
            };


        PhotonNetwork.CurrentRoom
            .SetCustomProperties(
                properties
            );


        Debug.Log(
            "Simulation reset requested."
        );
    }


    // =========================================================
    // ROOM PROPERTY UPDATE
    // =========================================================

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged
    )
    {
        /*
         * Whenever the Master changes the synchronized
         * simulation state, Photon sends the updated
         * room properties to every client.
         */

        LoadStateFromRoom();


        Debug.Log(
            $"Simulation clock updated: " +
            $"Started={hasStarted}, " +
            $"Running={isRunning}, " +
            $"Elapsed={ElapsedTime:F2}"
        );
    }


    // =========================================================
    // LOAD CURRENT STATE FROM PHOTON ROOM
    // =========================================================

    private void LoadStateFromRoom()
    {
        if (!PhotonNetwork.InRoom)
            return;


        if (PhotonNetwork.CurrentRoom == null)
            return;


        Hashtable properties =
            PhotonNetwork.CurrentRoom
                .CustomProperties;


        // -----------------------------------------------------
        // HAS STARTED
        // -----------------------------------------------------

        if (properties.TryGetValue(
            PROP_HAS_STARTED,
            out object startedValue))
        {
            hasStarted =
                (bool)startedValue;
        }
        else
        {
            hasStarted =
                false;
        }


        // -----------------------------------------------------
        // IS RUNNING
        // -----------------------------------------------------

        if (properties.TryGetValue(
            PROP_IS_RUNNING,
            out object runningValue))
        {
            isRunning =
                (bool)runningValue;
        }
        else
        {
            isRunning =
                false;
        }


        // -----------------------------------------------------
        // START NETWORK TIME
        // -----------------------------------------------------

        if (properties.TryGetValue(
            PROP_START_TIME,
            out object startValue))
        {
            startNetworkTime =
                System.Convert.ToDouble(
                    startValue
                );
        }
        else
        {
            startNetworkTime =
                0;
        }


        // -----------------------------------------------------
        // PAUSED ELAPSED TIME
        // -----------------------------------------------------

        if (properties.TryGetValue(
            PROP_PAUSED_ELAPSED,
            out object pausedValue))
        {
            pausedElapsedTime =
                System.Convert.ToDouble(
                    pausedValue
                );
        }
        else
        {
            pausedElapsedTime =
                0;
        }
    }

    private float debugTimer = 0f;

    private void Update()
    {
        if (!hasStarted)
            return;

        debugTimer += Time.deltaTime;

        if (debugTimer >= 2f)
        {
            debugTimer = 0f;

            Debug.Log(
                $"SIM TIME: {ElapsedTime:F2} | " +
                $"PHOTON TIME: {PhotonNetwork.Time:F2} | " +
                $"MASTER: {PhotonNetwork.IsMasterClient}"
            );
        }
    }

}