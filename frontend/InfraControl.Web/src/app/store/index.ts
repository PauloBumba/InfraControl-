import { configureStore } from "@reduxjs/toolkit";
import { persistStore, persistReducer } from 'redux-persist';
import storage from 'redux-persist/lib/storage';
import { uiReducer } from "./slices/uiSlice";
import { navigationReducer } from "./slices/navigationSlice";
import authReducer from "../../redux/slices/authSlice";

const persistConfig = {
  key: 'root',
  storage,
  whitelist: ['auth'], // Persistir apenas o slice auth
};

const persistedAuthReducer = persistReducer(persistConfig, authReducer);

export const store = configureStore({
  reducer: {
    ui: uiReducer,
    navigation: navigationReducer,
    auth: persistedAuthReducer,
  },
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware({
      serializableCheck: {
        ignoredActions: ['persist/PERSIST', 'persist/REHYDRATE'],
      },
    }),
});

export const persistor = persistStore(store);

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
