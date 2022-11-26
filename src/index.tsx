import './firebaseSetup';
import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.scss';
import App from './App/App';
import {onRenderCallback} from "./profiler";

const root = ReactDOM.createRoot(
  document.getElementById('root') as HTMLElement
);

root.render(
    <React.Profiler id="app" onRender={onRenderCallback}>
        <App/>
    </React.Profiler>
);
