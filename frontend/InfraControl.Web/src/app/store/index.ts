import { configureStore } from "@reduxjs/toolkit";
import {
  FLUSH,
  PAUSE,
  PERSIST,
  PURGE,
  REGISTER,
  REHYDRATE,
  persistReducer,
  persistStore,
} from "redux-persist";
import storage from "redux-persist/lib/storage";
import { uiReducer } from "./slices/uiSlice";
import { navigationReducer } from "./slices/navigationSlice";
import { authReducer, type AuthState } from "./slices/authSlice";

/**
 * F14-T008: persistência MÍNIMA do estado de autenticação.
 *
 * Só `user` (nome/e-mail/roles para exibição) vai para o localStorage, para
 * não piscar a tela no F5. Não entram:
 *  - flags de sessão (`status`): no boot o app SEMPRE revalida com GET /auth/me;
 *  - `pending`/`error`: estado transitório;
 *  - qualquer token, refresh token ou credencial (a sessão é o cookie HttpOnly
 *    do backend — inacessível ao JavaScript).
 *
 * `version` + `migrate` invalidam estados gravados por versões antigas.
 */
const authPersistConfig = {
  key: "auth",
  storage,
  version: 1,
  whitelist: ["user" as const],
};

export const store = configureStore({
  reducer: {
    ui: uiReducer,
    navigation: navigationReducer,
    auth: persistReducer<AuthState>(authPersistConfig, authReducer),
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: {
        ignoredActions: [FLUSH, REHYDRATE, PAUSE, PERSIST, PURGE, REGISTER],
      },
    }),
});

export const persistor = persistStore(store);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
