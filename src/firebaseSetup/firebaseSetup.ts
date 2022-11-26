// Import the functions you need from the SDKs you need
import firebase from "firebase/app";
import 'firebase/auth';
import 'firebase/database';
import {useEffect, useState} from "react";
import type {auth as authUI} from "firebaseui"
import {PromiseCell, PromiseSource} from "../PromiseSource";
import EmailAuthProvider = firebase.auth.EmailAuthProvider;
import GoogleAuthProvider = firebase.auth.GoogleAuthProvider;
import User = firebase.User;

// It is ok to commit these values to the repository
// https://stackoverflow.com/a/37484053/55208
const firebaseConfig = {
  apiKey: "AIzaSyCQqX64wOostYL7ibvgRKZNEjwWJVHS0jg",
  authDomain: "splitrack.firebaseapp.com",
  projectId: "splitrack",
  storageBucket: "splitrack.appspot.com",
  messagingSenderId: "734317115813",
  appId: "1:734317115813:web:0970083d2013bf217b504c",
  databaseURL: "https://splitrack-default-rtdb.europe-west1.firebasedatabase.app/"
};

// Initialize Firebase
export const firebaseApp = firebase.initializeApp(firebaseConfig, "splitrack");

let anonymousUser: firebase.User | null;
const firebaseAuth = firebaseApp.auth();
firebaseAuth.onAuthStateChanged((user: firebase.User | null) => {
  if (user == null) {
    console.log("Missing user detected, will sign in anonymously")
    firebaseAuth.signInAnonymously().then(() => {
      console.log("signed in anonymously", firebaseAuth.currentUser);
      anonymousUser = firebaseAuth.currentUser;
    }).catch(error => {
      console.error("Failed to create/connect to anonymous account.", error.code, error.message);
    })
  }
});

export const uiConfig: authUI.Config = {
  signInFlow: "popup",
  signInOptions: [
    // anonymous authentication is also supported :)
    {
      provider: EmailAuthProvider.PROVIDER_ID,
      requireDisplayName: false,
    },
    GoogleAuthProvider.PROVIDER_ID,
  ],
  autoUpgradeAnonymousUsers: true,
  callbacks: {
    signInFailure: async (error: { code: string, credential: any }) => {
      // For merge conflicts, the error.code will be
      // 'firebaseui/anonymous-upgrade-merge-conflict'.
      if (error.code != 'firebaseui/anonymous-upgrade-merge-conflict') {
        return Promise.resolve();
      }
      if (anonymousUser != null) {
        await anonymousUser.delete();
      }
      await firebaseAuth.signInWithCredential(error.credential);
    },
    signInSuccessWithAuthResult: () => false
  }
};

export const enum SignInState {
  Pending = "pending",
  Anonymous = "anonymous",
  SignedIn = "signedIn",
}

function toSignInState(user: User | null): SignInState {
  if (user == null) {
    return SignInState.Anonymous;
  }
  if (user.isAnonymous) {
    return SignInState.Anonymous;
  }
  return SignInState.SignedIn;
}

export function useAuth() {
  const auth = firebaseApp.auth();
  const [signInState, setSignInState] = useState(() => toSignInState(auth.currentUser));
  const [uid, setUid] = useState<string | null>(() => auth.currentUser?.uid ?? null);
  useEffect(() => {
    return firebaseAuth.onAuthStateChanged(user => {
      console.log("Sign in state changed", toSignInState(user), user?.uid);
      setSignInState(toSignInState(user));
      setUid(user?.uid ?? null);
    });
  });

  return {signInState, uid};
}

const realtimeDbSource = new PromiseSource<firebase.database.Database>();
export const realtimeDb: PromiseCell<firebase.database.Database> = realtimeDbSource;
firebaseAuth.onAuthStateChanged((user: User | null) => {
  if (user == null) {
    if (realtimeDbSource.resolved) {
      console.log("Disconnecting from realtime database because there is no user session.");
      realtimeDbSource.reset();
    }
  } else {
    console.log("Realtime db available");
    realtimeDbSource.resolve(firebaseApp.database());
  }
});
